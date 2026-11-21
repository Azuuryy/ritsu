use axum::{
    Json, Router,
    extract::{Path, Query, State},
    http::StatusCode,
    routing::{get, put},
};
use sea_orm::{
    ColumnTrait, ConnectOptions, ConnectionTrait, Database, DatabaseConnection, EntityTrait,
    PaginatorTrait, QueryFilter, QueryOrder, QuerySelect, Schema, Set, prelude::*,
    sea_query::OnConflict,
};
use serde::Deserialize;
use serde_json::{Value, json};
use utoipa::{OpenApi, ToSchema};
use utoipa_swagger_ui::SwaggerUi;
use uuid::Uuid;

mod play {
    use sea_orm::entity::prelude::*;

    #[derive(Clone, Debug, PartialEq, DeriveEntityModel)]
    #[sea_orm(table_name = "plays")]
    pub struct Model {
        #[sea_orm(primary_key)]
        pub id: i32,
        #[sea_orm(unique)]
        pub client_id: Uuid,
        #[sea_orm(indexed)]
        pub beatmap: String,
        pub ruleset: i32,
        pub total_score: i64,
        pub mod_key: String,
        pub score: Json,
    }

    #[derive(Copy, Clone, Debug, EnumIter, DeriveRelation)]
    pub enum Relation {}
    impl ActiveModelBehavior for ActiveModel {}
}

mod profile {
    use sea_orm::entity::prelude::*;

    #[derive(Clone, Debug, PartialEq, DeriveEntityModel)]
    #[sea_orm(table_name = "profiles")]
    pub struct Model {
        #[sea_orm(primary_key)]
        pub id: i32,
        #[sea_orm(unique)]
        pub client_id: Uuid,
    }

    #[derive(Copy, Clone, Debug, EnumIter, DeriveRelation)]
    pub enum Relation {}
    impl ActiveModelBehavior for ActiveModel {}
}

#[derive(Deserialize, ToSchema)]
struct Submission {
    profile_id: Uuid,
    username: String,
    #[schema(value_type = Object)]
    score: Value,
}

#[derive(Deserialize)]
struct RankingQuery {
    mode: String,
    limit: Option<u64>,
    mods: Option<String>,
}

fn map_hash(hash: String) -> Result<String, StatusCode> {
    if hash.len() != 32 || !hash.bytes().all(|b| b.is_ascii_hexdigit()) {
        return Err(StatusCode::BAD_REQUEST);
    }
    Ok(hash.to_ascii_lowercase())
}

fn database_error(error: DbErr) -> StatusCode {
    eprintln!("Database error: {error}");
    StatusCode::INTERNAL_SERVER_ERROR
}

#[utoipa::path(
    put,
    path = "/api/v2/beatmaps/{beatmap}/solo/scores/{id}",
    summary = "Store a completed solo play",
    description = "Stores a finished play. Reusing the same score ID preserves the original entry.",
    params(
        ("beatmap" = String, Path, description = "32-character MD5 of the beatmap difficulty", example = "0123456789abcdef0123456789abcdef"),
        ("id" = Uuid, Path, description = "Client-generated score ID", example = "aabc58b6-0f35-4895-ae0b-c327e2b482ea"),
    ),
    request_body(content = Submission, example = json!({
        "profile_id": "83743027-efaf-42d5-bbcd-e0f17aa9428e",
        "username": "Local player",
        "score": {
            "passed": true,
            "ruleset_id": 0,
            "total_score": 111195,
            "accuracy": 0.7985,
            "max_combo": 61,
            "rank": "C",
            "ended_at": "2026-11-21T00:00:00Z",
            "mods": [{"acronym": "SOT", "settings": {"end_spacing": 0.4}}]
        }
    })),
    responses(
        (status = 204, description = "Score stored or already present"),
        (status = 400, description = "Invalid beatmap hash, score ID or score data"),
        (status = 415, description = "Content-Type must be application/json"),
        (status = 422, description = "JSON does not match the submission envelope"),
        (status = 500, description = "Database operation failed"),
    ),
    tag = "Scores",
)]
async fn submit(
    State(db): State<DatabaseConnection>,
    Path((beatmap, client_id)): Path<(String, Uuid)>,
    Json(body): Json<Submission>,
) -> Result<StatusCode, StatusCode> {
    let beatmap = map_hash(beatmap)?;
    let username = body.username.trim();
    let ruleset = body.score["ruleset_id"]
        .as_i64()
        .filter(|id| (0..=3).contains(id));
    let total_score = body.score["total_score"]
        .as_i64()
        .filter(|score| *score >= 0);
    let accuracy = body.score["accuracy"]
        .as_f64()
        .filter(|value| (0.0..=1.0).contains(value));
    let (Some(ruleset), Some(total_score), Some(_)) = (ruleset, total_score, accuracy) else {
        return Err(StatusCode::BAD_REQUEST);
    };
    if body.score["passed"] != true
        || username.is_empty()
        || username.chars().count() > 64
        || client_id.is_nil()
        || body.profile_id.is_nil()
    {
        return Err(StatusCode::BAD_REQUEST);
    }

    profile::Entity::insert(profile::ActiveModel {
        client_id: Set(body.profile_id),
        ..Default::default()
    })
    .on_conflict(
        OnConflict::column(profile::Column::ClientId)
            .do_nothing()
            .to_owned(),
    )
    .do_nothing()
    .exec(&db)
    .await
    .map_err(database_error)?;

    let profile_id = profile::Entity::find()
        .filter(profile::Column::ClientId.eq(body.profile_id))
        .one(&db)
        .await
        .map_err(database_error)?
        .ok_or(StatusCode::INTERNAL_SERVER_ERROR)?
        .id;
    let mut score = body.score;
    let mut acronyms: Vec<_> = score["mods"]
        .as_array()
        .into_iter()
        .flatten()
        .filter_map(|value| value["acronym"].as_str())
        .collect();
    acronyms.sort_unstable();
    let mod_key = acronyms.join(",");
    score["user_id"] = json!(profile_id);
    score["user"] = json!({"id": profile_id, "username": username});
    score["has_replay"] = json!(false);
    for field in ["beatmap", "beatmapset", "legacy_score_id"] {
        score.as_object_mut().unwrap().remove(field);
    }
    play::Entity::insert(play::ActiveModel {
        client_id: Set(client_id),
        beatmap: Set(beatmap),
        ruleset: Set(ruleset as i32),
        total_score: Set(total_score),
        mod_key: Set(mod_key),
        score: Set(score),
        ..Default::default()
    })
    .on_conflict(
        OnConflict::column(play::Column::ClientId)
            .do_nothing()
            .to_owned(),
    )
    .do_nothing()
    .exec(&db)
    .await
    .map_err(database_error)?;
    Ok(StatusCode::NO_CONTENT)
}

#[utoipa::path(
    get,
    path = "/api/v2/beatmaps/{beatmap}/scores",
    summary = "Get the global ranking for a difficulty",
    description = "Returns plays sorted by descending total score. The count covers all matching plays before the limit is applied.",
    params(
        ("beatmap" = String, Path, description = "32-character MD5 of the beatmap difficulty", example = "0123456789abcdef0123456789abcdef"),
        ("mode" = String, Query, description = "Ruleset: osu, taiko, fruits or mania", example = "osu"),
        ("limit" = Option<u64>, Query, description = "Number of results, limited to 1–100. Defaults to 50."),
        ("mods" = Option<String>, Query, description = "Sorted comma-separated acronyms to match exactly. An empty value selects No Mod.", example = "SOT"),
    ),
    responses(
        (status = 200, description = "Global ranking",
            body = Object, example = json!({
                "scores": [{
                    "id": 1, "ruleset_id": 0, "passed": true, "total_score": 111195,
                    "accuracy": 0.7985, "max_combo": 61, "rank": "C",
                    "mods": [{"acronym": "SOT", "settings": {"end_spacing": 0.4}}],
                    "user_id": 1, "user": {"id": 1, "username": "Local player"},
                    "ended_at": "2026-11-21T00:00:00Z", "has_replay": false
                }],
                "score_count": 1, "user_score": null
            })),
        (status = 400, description = "Invalid beatmap hash or query parameters"),
        (status = 500, description = "Database operation failed"),
    ),
    tag = "Scores",
)]
async fn ranking(
    State(db): State<DatabaseConnection>,
    Path(beatmap): Path<String>,
    Query(query): Query<RankingQuery>,
) -> Result<Json<Value>, StatusCode> {
    let beatmap = map_hash(beatmap)?;
    let ruleset = match query.mode.as_str() {
        "osu" => 0,
        "taiko" => 1,
        "fruits" => 2,
        "mania" => 3,
        _ => return Err(StatusCode::BAD_REQUEST),
    };
    let mut plays = play::Entity::find()
        .filter(play::Column::Beatmap.eq(beatmap))
        .filter(play::Column::Ruleset.eq(ruleset));
    if let Some(mod_key) = query.mods {
        plays = plays.filter(play::Column::ModKey.eq(mod_key));
    }
    let count = plays.clone().count(&db).await.map_err(database_error)?;
    let scores: Vec<_> = plays
        .order_by_desc(play::Column::TotalScore)
        .order_by_asc(play::Column::Id)
        .limit(query.limit.unwrap_or(50).clamp(1, 100))
        .all(&db)
        .await
        .map_err(database_error)?
        .into_iter()
        .map(|mut play| {
            play.score["id"] = json!(play.id);
            play.score
        })
        .collect();
    Ok(Json(
        json!({"scores": scores, "score_count": count, "user_score": null}),
    ))
}

#[derive(OpenApi)]
#[openapi(
    info(
        title = "Ritsu local score API",
        description = "Local solo score submissions and beatmap rankings."
    ),
    paths(submit, ranking),
    components(schemas(Submission))
)]
struct ApiDoc;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let url = std::env::var("RITSU_DATABASE_URL")
        .unwrap_or_else(|_| "sqlite://ritsu.sqlite?mode=rwc".to_owned());
    let mut options = ConnectOptions::new(url);
    options.max_connections(1).sqlx_logging(false);
    let db = Database::connect(options).await?;
    let backend = db.get_database_backend();
    let schema = Schema::new(backend);
    db.execute(
        backend.build(
            schema
                .create_table_from_entity(profile::Entity)
                .if_not_exists(),
        ),
    )
    .await?;
    db.execute(
        backend.build(
            schema
                .create_table_from_entity(play::Entity)
                .if_not_exists(),
        ),
    )
    .await?;
    let index = sea_orm::sea_query::Index::create()
        .if_not_exists()
        .name("plays_ranking")
        .table(play::Entity)
        .col(play::Column::Beatmap)
        .col(play::Column::Ruleset)
        .col(play::Column::TotalScore)
        .to_owned();
    db.execute(backend.build(&index)).await?;

    let app = Router::new()
        .route("/api/v2/beatmaps/{beatmap}/scores", get(ranking))
        .route("/api/v2/beatmaps/{beatmap}/solo/scores/{id}", put(submit))
        .with_state(db)
        .merge(SwaggerUi::new("/docs").url("/api-docs/openapi.json", ApiDoc::openapi()));
    let port = std::env::var("RITSU_SERVER_PORT")
        .unwrap_or_else(|_| "5123".into())
        .parse::<u16>()?;
    let listener = tokio::net::TcpListener::bind(("127.0.0.1", port)).await?;
    println!(
        "Ritsu score API listening on http://{}",
        listener.local_addr()?
    );
    axum::serve(listener, app).await?;
    Ok(())
}

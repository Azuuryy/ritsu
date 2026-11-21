// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.IO;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.Maintenance;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Picks a folder for Ritsu: replays to import (subfolders included), or where to export replays to.
    /// </summary>
    public partial class LocalFolderSelectScreen : DirectorySelectScreen
    {
        /// <summary>
        /// The folder picked last time (this session), to start from next time.
        /// </summary>
        private static DirectoryInfo? lastFolder;

        private readonly string header;
        private readonly Action<DirectoryInfo> selected;

        public LocalFolderSelectScreen(string header, Action<DirectoryInfo> selected)
        {
            this.header = header;
            this.selected = selected;
        }

        protected override OverlayActivation InitialOverlayActivationMode => OverlayActivation.Disabled;

        public override LocalisableString HeaderText => header;

        protected override DirectoryInfo? InitialPath => lastFolder != null && lastFolder.Exists ? lastFolder : null;

        protected override bool IsValidDirectory(DirectoryInfo? info) => info != null;

        protected override void OnSelection(DirectoryInfo directory)
        {
            lastFolder = directory;
            selected(directory);
            this.Exit();
        }
    }
}

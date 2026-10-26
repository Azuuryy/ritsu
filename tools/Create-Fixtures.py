from pathlib import Path
import zipfile, wave, io
root = Path(__file__).resolve().parent / 'fixtures'
root.mkdir(exist_ok=True)
audio=io.BytesIO()
with wave.open(audio,'wb') as wav:
    wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(22050)
    wav.writeframes(b'\0\0' * (22050*9))
chart='''osu file format v14

[General]
AudioFilename: validation.wav
AudioLeadIn: 0
PreviewTime: 0
Mode: 0

[Metadata]
Title:Ritsu offline validation
Artist:Ritsu
Creator:Ritsu validation
Version:Local
Source:
Tags:ritsu offline validation

[Difficulty]
HPDrainRate:0
CircleSize:4
OverallDifficulty:5
ApproachRate:5
SliderMultiplier:1.4
SliderTickRate:1

[Events]

[TimingPoints]
0,500,4,1,0,70,1,0

[HitObjects]
256,192,1000,1,0,0:0:0:0:
128,192,2000,1,0,0:0:0:0:
384,192,3000,1,0,0:0:0:0:
256,192,4000,1,0,0:0:0:0:
128,192,5000,2,0,L|384:192,1,140
256,192,6000,8,0,7000,0:0:0:0:
'''
with zipfile.ZipFile(root/'ritsu-validation.osz','w',zipfile.ZIP_DEFLATED) as z:
    z.writestr('validation.wav',audio.getvalue())
    z.writestr('Ritsu - validation (Ritsu validation) [Local].osu',chart)
with zipfile.ZipFile(root/'ritsu-validation.osk','w',zipfile.ZIP_DEFLATED) as z:
    z.writestr('skin.ini','[General]\nName: Ritsu validation skin\nAuthor: Ritsu validation\nVersion: 2.7\n[Colours]\nCombo1: 140,90,230\nCombo2: 220,60,160\n')
print(root)

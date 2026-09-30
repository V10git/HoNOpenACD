# ⚔️ HoN Open ACD
![Screenshot](assets/screenshot.png)

This is open source **UniCheat.NET** based x64 application for modify **Heroes of Newerth** gameplay.  

Current features:
 - Control player camera distance

### 💾 Download binary releases
https://github.com/V10git/HoNOpenACD/releases

Closed source, full featured version **HoN ACD** can be downloaded from official page https://v10.name/acd/

### 🚀 Usage
* **Extract** `HoNOpenACD.exe` to folder in your computer
* **Start the game** and wait until you reach the main lobby or the game logic loads.
* **Run** `HoNOpenACD.exe`
* **Configure settings (Optional)**: After the first launch, a `config.json` file will automatically be created in the same folder. You can edit this file to change the maximum camera distance (e.g., `"MaxCameraDistance": 4500`).

### 💡 Build for legacy HoN
`git clone https://github.com/V10git/HoNOpenACD.git`  
`dotnet build -c Release HoNOpenACD\HoNOpenACD.csproj`

### 💡 Build for Reborn
`git clone https://github.com/V10git/HoNOpenACD.git`  
`dotnet build -c Release HoNOpenACD\HoNOpenACD.csproj /p:DefineConstants="BUILD_REBORN"`

### 💡 Custom HoN scripts
For create custom script, look [example script](ExampleScript/).\
You can load custom external scripts by adding it manually to config.json  
**Example:**
```json
  "UniCheat": {
    "AllowExternalScripts": true,
    "Scripts": {
      "CameraDistance": {
        "Enable": true
      },
      "ExampleScript": {
        "Enable": true,
        "External": {
          "Filename": "ExampleScript.dll",
          "FullClassName": "ExampleScript.ExampleScript"
        }
      }
    }
  }
```

[UniCheat.Net docs](UniCheatNET/README.md)


### 📬 Contacts
ACD  Telegram: https://t.me/ACDFeedback

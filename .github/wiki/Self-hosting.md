## Self-hosting

:warning:️ **Are you sure you want to self-host your bot?** :warning:️\
The version we host has no restrictions. Consider using it.

If you still want to continue - please **_use the self-hosted version for personal use only_**. Don't promote it. Be understanding.

Also, we are not responsible for the code and its operability, updatability, backward compatibility, etc. For all questions - write to our support server.

1. Compile the sources using [this page](Compiling-sources.md).
2. Install, launch and configure [lavalink](https://github.com/lavalink-devs/Lavalink)
3. Navigate to binaries folder (typically `Enliven/bin/Release/net10.0/`) and launch the bot (`./Enliven` for linux or `Enliven.exe` for Windows)
4. Copy `appsettings.json` to `appsettings.Production.json` and edit it. Here is examples for sections:

   ```json
   "LavalinkNodes": [
     {
       "RestUri": "http://localhost:8082",
       "WebSocketUri": "ws://localhost:8082",
       "Password": "password",
       "Name": null
     }
   ]
   ```

   ```json
   "Instances": [
     {
       "Name": "Main",
       "BotToken": "PLACE BOT TOKEN HERE",
       "LavalinkNodes": [ ],
       "Modules": ["!logging"]
     }
   ]
   ```

## Self-hosting FAQ

#### How to add lavalink nodes to bot

Edit `LavalinkNodes` variable in your config file:

```json
"LavalinkNodes": [
    {
    "RestUri": "http://localhost:8081",
    "WebSocketUri": "ws://localhost:8081",
    "Password": "mypass",
    "Name": "Name will be displayed in player embed"
    },
    {
    "RestUri": "http://localhost:8082",
    "WebSocketUri": "ws://localhost:8083",
    "Password": "mypass",
    "Name": null
    }
]
```

#### How to change bot messages test

1. Edit localization files in `Common/Localization/`. (Consider using document search to find what you want to change)
2. Build project via [compile sources page](compiling-sources)
3. Restart bot

#### How to update bot

1. Update repo:

```plaintext
git pull
```

2. Build project via [compile sources page](Compiling-sources.md)

#### Emoji missing

Discord bots work as users with nitro. They can use all emojis from the servers they are on. The default emoji are on my private development server. There are 2 ways for emoji to work properly in a bot:

1. Add your bot to our server with emoji

* Join our support server (link in [README.md](https://github.com/EnlivenBot/Enliven))
* Send a direct message with your bot invite link to any user with ADMIN role (prefer `skproch`)
* After that, we will add your bot to the server with emoji and the emoji that are specified in the default config will work for you.

2. Change emojis in config

* Edit variables in `Config/*Emoji.json` files  

#### Cannot resolve VK.com music

With the error:

> System.ArgumentException: Invalid file. Cannot load file "blablabla/index.m3u8" 

Seems like this is happens on linux. You should install **nscd.** For debian based distros:

```shell
sudo apt install nscd
```
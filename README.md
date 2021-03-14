<table align="center" bgcolor="brown"><tr><td align="center" width="9999">

## Old bot owner account is hacked now. 
Please kick old bot user and,  if you want to continue using the bot, [add a new bot](https://discord.com/oauth2/authorize?client_id=801159808929497208&scope=bot&permissions=1110764608). (Enliven#5499)  

All your data will be kept and transfer to new bot
</td></tr>
</table>
<table align="center"><tr><td align="center" width="9999">
<img src="https://cdn.discordapp.com/avatars/606760964183949314/e87c138f296e53e63fd55114eff36dcf.png" align="center" alt="Project icon">

# Enliven

  <p align="center">
  
  [![Discord Bots](https://top.gg/api/widget/status/606760964183949314.svg)](https://top.gg/bot/606760964183949314)
  [![Discord Bots](https://top.gg/api/widget/upvotes/606760964183949314.svg)](https://top.gg/bot/606760964183949314)
  [![Discord Bots](https://top.gg/api/widget/owner/606760964183949314.svg)](https://top.gg/bot/606760964183949314)
  
  </p>

> Powerful music and logging bot for Discord  

**Fully free** [Discord](https://discord.com/) bot with Embed music playback, emoji control, various music sources (including livestreams) and more.  
The best opportunities for logging messages.
</td></tr>
<tr><td align="center" width="9999">

#### Join our support server:

[![Discord support server](https://discordapp.com/api/guilds/706098979938500708/widget.png?style=banner3)](https://discord.gg/zfBffbBbCp)

</td></tr>
</table>

## Features
 * Music player
   * Embed playback control
     * Emoji control
     * Progress display
     * Queue display
     * Request history
     * This and more in one control message:  
     <img src="https://gitlab.com/skprochlab/nJMaLBot/-/wikis/uploads/80b7037668f4762bcb01017aa7114264/Screenshot_5.png" />
   * Various music sources
     * Youtube, Twitch, SoundCloud and all [supported by youtube-dl sites](https://ytdl-org.github.io/youtube-dl/supportedsites.html)
     * Spotify
     * Live streams
   * Bass boost support
   * Custom playlists
   * Multiple player nodes
 * Logging
   * Collects a full changes history
   * With attachment links
   * Display all message changes in one place
   * Fully configurable
   * Export changes history to image
     * Highlighting deletions and additions
     * Full support for rendering:
       * Emote
       * Custom emoji
       * Links
       * Mentions
     * [Example result (warning: *large image*)](https://cdn.discordapp.com/attachments/667271058461687809/722864116741439629/History-672479528634941440-722861553564778588.png)
 * Multilingual

## Getting started

1. You need to add the bot to your server using [this link](https://discord.com/oauth2/authorize?client_id=801159808929497208&scope=bot&permissions=1110764608)
2. To find out all available commands use `&help`

### Initial Configuration

* The default prefix for all commands is `&` (*or bot user mention*) by default. Set a custom server prefix with the `setprefix` command!  
* You can choose the language for the bot using the `language` command.  
* You can set up message logging using the `logging` command. 

## Contributing

If you'd like to contribute, please fork the repository and use a feature
branch. Pull requests are warmly welcome.

## Links

- [Project homepage](https://gitlab.com/skprochlab/nJMaLBot)
- [Repository](https://gitlab.com/skprochlab/nJMaLBot)
- [Issue tracker](https://gitlab.com/skprochlab/nJMaLBot/-/issues)
- [Discord support server](https://discord.gg/zfBffbBbCp)  
- Enliven on discord bots lists:
  - Links will appear here as soon as I regain access to my account. The official source of information is this gitlab.
  
[comment]: <> (  - [top.gg]&#40;https://top.gg/bot/606760964183949314&#41;)

[comment]: <> (  - [discordbotlist.com]&#40;https://discordbotlist.com/bots/enliven&#41;)

[comment]: <> (  - [bots.ondiscord.xyz]&#40;https://bots.ondiscord.xyz/bots/606760964183949314&#41;)

[comment]: <> (  - [discord.bots.gg]&#40;https://discord.bots.gg/bots/606760964183949314&#41;)
  
## Help us
You can help by making a contributing or by voting on the following sites:
- Links will appear here as soon as I regain access to my account. The official source of information is this gitlab.

[comment]: <> (- [top.gg]&#40;https://top.gg/bot/606760964183949314&#41; &#40;vote every 12 hours, write a review&#41;)

[comment]: <> (- [discordbotlist.com]&#40;https://discordbotlist.com/bots/enliven&#41; &#40;vote every 12 hours&#41;)

[comment]: <> (- [bots.ondiscord.xyz]&#40;https://bots.ondiscord.xyz/bots/606760964183949314&#41; &#40;write a review&#41;)

## Compiling sources
1. Clone this repo:
```
git clone https://github.com/AvaloniaUtils/ShowMeTheXaml.Avalonia.git
```
2. Navigate to repo folder
3. Fetch all submodules:
```
git submodule update --init --recursive
```
4. Install .NetCore 3.1
5. Compile project:
```
dotnet build
```

## Self-hosting
⚠️ **Are you sure you want to self-host your bot?** ⚠️  
The version we host has no restrictions. Consider using it.

If you still want to continue - please ***use the self-hosted version for personal use only***. Don't promote it. Be understanding.

Also, we are not responsible for the code and its operability, updatability, backward compatibility, etc. For all questions - write to our support server.

1. Compile the sources using the guide above.
2. Install, launch and configure [lavalink](https://github.com/Frederikam/Lavalink#server-configuration)
3. Navigate to binaries folder (typically `Enliven/bin/Release/dotnetcore3.1/`) and launch the bot (`./Enliven` for linux or `Enliven.exe` for Windows)
4. After first launch bot would generate config file (`Config/config.json`). Edit it.
### Self-hosting FAQ
#### How to add lavalink nodes to bot
Edit `LavalinkNodes` variable in `Config/config.json` file:
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
#### How to change text on the embeds
1. Edit localization files in `Common/Localization/`. (Consider using document search to find what you want to change)
2. Build project: `dotnet build` (need to be executed in repository root)
#### How to update bot
1. Update repo:
```
git pull
git submodule update --recursive
```
2. Build project:
```
   dotnet build
```
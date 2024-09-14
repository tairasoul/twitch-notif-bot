# GrassGuyC#

fuck you *rewrites your bot in c#*

vainstar said i should rewrite [his bot](https://github.com/vainstar1/GrassGuyBot) in c#, so i did (mostly, i'm splitting off reaction roles n the like to a different bot) :3

config.json.example and twitch.credentials.json.example are there to show you how the two json files are supposed to look, they're called the exact same but without .example

the bot finds config.json and twitch.credentials.json in the directory you're running the bot executable from, database_path is relative to that same directory too.

say you're in "C:/Users/user/Documents/bot", it will look for "C:/Users/user/Documents/bot/config.json" and "C:/Users/user/Documents/bot/twitch.credentials.json"

and if your database_path is database.db, it will save the database at "C:/Users/user/Documents/bot/database.db"

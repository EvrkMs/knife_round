# [Knife Round]

## [Requirement]:

[Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)

[CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases)

## [Description]
Knife round after warm-up. At the end of the warm-up, the voting of the winning team begins with the commands !switch and !stay

## [Config]

```json
  // Give Armor On Knife Round?
  // (0) = No
  // (1) = Give Armor
  // (2) = Give Armor + Helmet
  "GiveArmorOnKnifeRound": 1,

  // Freeze Players On Voting
  "FreezeOnVote": true,

  // Block Team Changing (To Avoid Loser Switch To Winner Team And Vote)
  "BlockTeamChangeOnVoteAndKnife": true,

  // Knife Round Time (In Mins)
  "KnifeRoundTimer": 2,

  // After Winner Pick Team How Many Restart Would You Like
  "AfterWinningRestartXTimes": 3,

  // Here you need to indicate under what name the plugin will write to the chat.
  "ChatDisplayName": "AVA",

  // It is necessary to indicate how long the team will take before the knife round.
  "TeamIntroTimeKnifeStart": 5,

  // It is not working, sorry
  "TeamIntroTimeAfterKnife": 5,

  // Message about the start of a knife round
  "StartMessage": "Knives ready?",

  // Message about the start of voting for changing sides, you can say "Voting has started"
  "VoteMessage": "Voting has started",

  // The description of the !switch command will be something like this, "!switch - (your description like "switch sides")"
  "SwitchMessage": "Switch sides",

  // The description of the !stay command will be something like this, "!stay - (your description like "Do not change sides)"
  "StayMessage": "Do not change sides",

  // Eternal warm-up after the knife round (done if you absolutely need to choose a side (there is no random at the end of the warm-up yet))
  // true = on
  // false = off
  "PauseWarmupTimerAfterKnife": true,

  //Time for warm-up if PuaseWarmupTimerAfterKnif is set to false
  "WarmupTimeAfterKnife": 60,

  // How do you say "votes" in your language?
  "LocalizedVote": "votes",

  //-----------------------------------------------------------------------------------------
  "ConfigVersion": 1
```


## [Initial project]
I started with the original project https://github.com/oqyh/cs2-Knife-Round, but I've modified it to be closer to what's on Faceit. However, I'm currently unable to adapt it to a system where there is a captain.

/*
 * Hypnos: Improvements to the Stardew Valley sleeping experience
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using xTile.Layers;
using xTile.Tiles;

namespace StardewHypnos
{
    class ModEntry : Mod
    {
        private ModConfig config;

        private bool shouldAsk = false;

        private Dictionary<string, List<string>> OwnerByWarp = new Dictionary<string, List<string>>
        {
            {
                "AnimalShop",
                new List<string>
                {
                    "Marnie",
                    "Shane"
                }
            },
            {
                "SeedShop",
                new List<string>
                {
                    "Abigail",
                    "Pierre",
                    "Caroline"
                }
            },
            {
                "Trailer",
                new List<string>
                {
                    "Penny",
                    "Pam"
                }
            },
            {
                "HaleyHouse",
                new List<string>
                {
                    "Haley",
                    "Emily"
                }
            },
            {
                "LeahHouse",
                new List<string>
                {
                    "Leah"
                }
            },
            {
                "ScienceHouse",
                new List<string>
                {
                    "Maru",
                    "Robin",
                    "Demetrius",
                    "Sebastian"
                }
            },
            {
                "JoshHouse",
                new List<string>
                {
                    "Alex",
                    "George",
                    "Evelyn"
                }
            },
            {
                "ElliottHouse",
                new List<string>
                {
                    "Elliott"
                }
            },
            {
                "SamHouse",
                new List<string>
                {
                    "Sam",
                    "Kent",
                    "Jodi",
                }
            },
            {
                "ManorHouse",
                new List<string>
                {
                    "Lewis"
                }
            },
            {
                "SebastianRoom",
                new List<string>
                {
                    "Sebastian"
                }
            },
            {
                "HarveyRoom",
                new List<string>
                {
                    "Harvey"
                }
            },
            {
                "Saloon",
                new List<string>
                {
                    "Gus"
                }
            },
            {
                "Tent",
                new List<string>
                {
                    "Linus"
                }
            },
            {
                "Blacksmith",
                new List<string>
                {
                    "Clint"
                }
            }
        };

        public override void Entry(IModHelper helper)
        {
            // Load configuration
            config = helper.ReadConfig<ModConfig>();

            // Add custom warps, remove blacklisted ones
            if (config.CustomOwnerByWarp != null && config.CustomOwnerByWarp.Count > 0)
                config.CustomOwnerByWarp.ToList().ForEach(x => OwnerByWarp.Add(x.Key, x.Value));
            if (config.BlacklistedWarps != null && config.BlacklistedWarps.Count > 0)
                config.BlacklistedWarps.ForEach(x => OwnerByWarp.Remove(x));

            // Right-Click Hook: Enter Bachalor(ette) or friends' homes at any time
            if (config.KeepFriendDoorsOpen)
                helper.Events.Input.ButtonPressed += OnButtonPressed;

            // Warp Hook: Trigger OnUpdateTicked (for sleeping) when necessary
            helper.Events.Player.Warped += OnPlayerWarped;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // Is the Tile under the player a bed?
            Layer frontLayer = Game1.currentLocation.Map?.GetLayer("Front");
            if (frontLayer == null)
                return;

            Vector2 tileLocation = Game1.player.getTileLocation();
            Tile tile = frontLayer.Tiles[Convert.ToInt32(tileLocation.X), Convert.ToInt32(tileLocation.Y)];

            // Failsafe: should never happen..?
            if (tile == null)
                return;

            if (IsBedTile(tile))
            {
                // Sleep!
                if (!Game1.newDay && Game1.shouldTimePass() && shouldAsk)
                {
                    // Don't ask again before leaving the Bed tile
                    shouldAsk = false;
                    Game1.currentLocation.createQuestionDialogue(
                        Game1.content.LoadString("Strings\\Locations:FarmHouse_Bed_GoToSleep"),
                        Game1.currentLocation.createYesNoResponses(),
                        delegate (Farmer _, string answer)
                        {
                            if (answer == "Yes")
                            {
                                if (Game1.IsMultiplayer)
                                {
                                    // Special handling for MP
                                    Game1.player.team.SetLocalReady("sleep", true);
                                    Game1.dialogueUp = false;
                                    Game1.activeClickableMenu = new ReadyCheckDialog("sleep", true, delegate (Farmer who)
                                    {
                                        /*
                                         * This is a bit frustrating. Seems like isInBed needs to be set
                                         * as late as possible or it'll get.. overwritten? Anyways, if
                                         * isInBed is false the following doSleep call will cause a hang.
                                         */
                                        Game1.player.isInBed.Set(true);
                                        Helper.Reflection.GetMethod(Game1.currentLocation, "doSleep").Invoke();
                                    }, delegate (Farmer who)
                                    {
                                        if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ReadyCheckDialog)
                                        {
                                            (Game1.activeClickableMenu as ReadyCheckDialog).closeDialog(who);
                                        }
                                        who.timeWentToBed.Value = 0;
                                    });
                                }
                                else
                                {
                                    Helper.Reflection.GetMethod(Game1.currentLocation, "startSleep").Invoke();
                                }
                            }
                        });
                }
            }
            else
            {
                // Left the Sleeping dialog tile
                shouldAsk = true;
            }
        }

        private void OnPlayerWarped(object sender, WarpedEventArgs e)
        {
            // Warped to a location "owned" by a Bachelor(ette) or a friend?
            OwnerByWarp.TryGetValue(e.NewLocation.Name, out List<string> npcsInWarp);

            switch (config.MinimumRelationship)
            {
                case MinimumRelationshipType.OnlyPartners:
                    // No reason to handle this if the Farmer isn't in a relationship with any of the NPCs
                    if (!IsFarmerInRelationship(npcsInWarp))
                    {
                        Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
                        return;
                    }
                    break;
                case MinimumRelationshipType.Friends:
                    // No reason to handle this if the Farmer isn't in a friendship with any of the NPCs
                    if (!IsFarmerInFriendship(npcsInWarp))
                    {
                        Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
                        return;
                    }
                    break;
                case MinimumRelationshipType.Everyone:
                    // No operation: everyone's beds should be "sleepable"
                    break;
                default:
                    break;
            }

            Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private bool IsBedTile(Tile tile)
        {
            string tileSheet = tile.TileSheet.ImageSource.Replace("Maps\\", "").Replace("Maps/", "");

            if (tileSheet == "townInterior")
            {
                int[] bedIndexes =
                {
                    384, 386, 390, 448, 450, 454, 836, 1123, 1294
                };

                foreach (int bedIndex in bedIndexes)
                {
                    if (tile.TileIndex == bedIndex)
                        return true;
                }
            }
            else if (tileSheet == "ElliottHouseTiles")
            {
                if (tile.TileIndex == 25)
                    return true;
            }

            return false;
        }

        private bool IsFarmerInFriendship(List<string> npcs)
        {
            if (npcs == null || npcs.Count == 0)
                return false;

            // Is the Farmer friends enough with at least one of the passed NPCs?
            foreach (string npc in npcs)
            {
                Game1.player.friendshipData.TryGetValue(npc, out Friendship friendship);

                // 1 Heart = 250 Points
                if (friendship.Points >= 250 * config.MinimumFriendshipHearts)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsFarmerInRelationship(List<string> npcs)
        {
            if (npcs == null || npcs.Count == 0)
                return false;

            // Is the Farmer dating, engaged or married with at least one of the passed NPCs?
            foreach (string npc in npcs)
            {
                Game1.player.friendshipData.TryGetValue(npc, out Friendship friendship);

                if (friendship.IsDating() || friendship.IsEngaged() || friendship.IsMarried())
                {
                    return true;
                }
            }

            return false;
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Only accept ingame events with an "unlocked" Farmer
            if (!Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            // Only accept right-click button events
            if (e.Button != SButton.MouseRight)
                return;

            // Get selected Tile and maybe its Action property
            Vector2 tilePosition = e.Cursor.GrabTile;
            string tileProp = Game1.currentLocation.doesTileHaveProperty(
                Convert.ToInt32(tilePosition.X), Convert.ToInt32(tilePosition.Y), "Action", "Buildings");

            if (tileProp == null || tileProp.Length == 0)
                return;

            // Skips "Action", takes only arguments
            string[] propArgs = tileProp.Split(' ');

            // Return if tile doesn't have an (outdoors?) door warp
            if (propArgs[0] != "LockedDoorWarp")
                return;

            string warpLocation = propArgs[3];
            OwnerByWarp.TryGetValue(warpLocation, out List<string> npcsInHouse);

            switch (config.MinimumRelationship)
            {
                case MinimumRelationshipType.OnlyPartners:
                    if (!IsFarmerInRelationship(npcsInHouse))
                        return;
                    break;
                case MinimumRelationshipType.Friends:
                case MinimumRelationshipType.Everyone:
                    /* 
                     * 1. Only open doors at all times for people in friendships with you
                     * 2. We don't want to open doors for every ingame location!
                     */
                    if (!IsFarmerInFriendship(npcsInHouse))
                        return;
                    break;
                default:
                    return;
            }

            // Passed check: open friend or partner door
            Rumble.rumble(0.15f, 200f);
            Game1.player.completelyStopAnimatingOrDoingAction();
            Game1.currentLocation.playSoundAt("doorClose", Game1.player.getTileLocation());
            Game1.warpFarmer(warpLocation, Convert.ToInt32(propArgs[1]), Convert.ToInt32(propArgs[2]), false);

            // Exit with event suppression (avoid game handling this event)
            Helper.Input.Suppress(SButton.MouseRight);
        }
    }
}


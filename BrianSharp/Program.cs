using System;
using System.Linq;
using BrianSharp.Common;
using LeagueSharp;
using LeagueSharp.Common;
using LeagueSharp.Common.Data;
using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp
{
    internal class Program
    {
        public static Obj_AI_Hero Player;
        public static Spell Q, Q2, W, W2, E, E2, R;
        public static SpellSlot Flash, Smite, Ignite;
        public static Items.Item Tiamat, Hydra, Youmuu, Zhonya, Seraph, Sheen, Iceborn, Trinity;
        public static Menu MainMenu;
        public static String PlayerName;

        private static void Main(string[] args)
        {
            if (args == null)
            {
                return;
            }
            if (Game.Mode == GameMode.Running)
            {
                OnGameStart(new EventArgs());
            }
            Game.OnGameStart += OnGameStart;
        }

        private static void OnGameStart(EventArgs args)
        {
            Player = ObjectManager.Player;
            PlayerName = Player.ChampionName;
            var plugin = Type.GetType("BrianSharp.Plugin." + PlayerName);
            if (plugin == null)
            {
                Helper.AddNotif(string.Format("[Brian Sharp] - {0}: Not support !", PlayerName), 2000);
                return;
            }
            MainMenu = new Menu("Brian Sharp", "BrianSharp", true);
            var infoMenu = new Menu("Info", "Info");
            {
                infoMenu.AddItem(new MenuItem("Author", "Author: Brian"));
                infoMenu.AddItem(new MenuItem("Paypal", "Paypal: dcbrian01@gmail.com"));
                MainMenu.AddSubMenu(infoMenu);
            }
            TargetSelector.AddToMenu(MainMenu.AddSubMenu(new Menu("Target Selector", "TS")));
            Orbwalk.AddToMainMenu(MainMenu);
            Activator.CreateInstance(plugin);
            Helper.AddItem(MainMenu.SubMenu(PlayerName + "_Plugin").SubMenu("Misc"), "UsePacket", "Use Packet To Cast");
            Tiamat = ItemData.Tiamat_Melee_Only.GetItem();
            Hydra = ItemData.Ravenous_Hydra_Melee_Only.GetItem();
            Youmuu = ItemData.Youmuus_Ghostblade.GetItem();
            Zhonya = ItemData.Zhonyas_Hourglass.GetItem();
            Seraph = ItemData.Seraphs_Embrace.GetItem();
            Sheen = ItemData.Sheen.GetItem();
            Iceborn = ItemData.Iceborn_Gauntlet.GetItem();
            Trinity = ItemData.Trinity_Force.GetItem();
            Flash = Player.GetSpellSlot("summonerflash");
            foreach (var spell in
                Player.Spellbook.Spells.Where(
                    i =>
                        i.Name.ToLower().Contains("smite") &&
                        (i.Slot == SpellSlot.Summoner1 || i.Slot == SpellSlot.Summoner2)))
            {
                Smite = spell.Slot;
            }
            Ignite = Player.GetSpellSlot("summonerdot");
            MainMenu.AddToMainMenu();
            Helper.AddNotif(string.Format("[Brian Sharp] - {0}: Loaded !", PlayerName), 2000);
        }
    }
}
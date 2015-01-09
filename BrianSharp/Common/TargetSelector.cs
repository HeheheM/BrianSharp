using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;

namespace BrianSharp.Common
{
    public class TargetSelector
    {
        private readonly string[] AD =
        {
            "Ashe", "Caitlyn", "Corki", "Draven", "Ezreal", "Graves", "Jinx", "Kalista", "KogMaw", "Lucian", "MissFortune", "Quinn", "Sivir", "Talon",
            "Tristana", "Twitch", "Urgot", "Varus", "Vayne", "Yasuo", "Zed"
        };
        private readonly string[] AP =
        {
            "Ahri", "Akali", "Anivia", "Annie", "Azir", "Brand", "Cassiopeia", "Diana", "FiddleSticks", "Fizz", "Heimerdinger", "Karthus", "Kassadin",
            "Katarina", "Kayle", "Kennen", "Leblanc", "Lissandra", "Lux", "Malzahar", "Mordekaiser", "Morgana", "Nidalee", "Orianna", "Ryze", "Swain",
            "Syndra", "Teemo", "TwistedFate", "Veigar", "Velkoz", "Viktor", "Vladimir", "Xerath", "Ziggs", "Zyra"
        };
        private readonly string[] Support = { "Blitzcrank", "Janna", "Karma", "Leona", "Lulu", "Nami", "Sona", "Soraka", "Thresh", "Zilean" };
        private readonly string[] Bruiser =
        {
            "Aatrox", "Darius", "Elise", "Evelynn", "Fiora", "Gangplank", "Gnar", "Gragas", "Irelia", "JarvanIV", "Jax", "Jayce", "Khazix", "LeeSin",
            "MasterYi", "Nocturne", "Olaf", "Pantheon", "Poppy", "RekSai", "Renekton", "Rengar", "Riven", "Rumble", "Shaco", "Shyvana", "Sion", "Trundle",
            "Tryndamere", "Udyr", "Vi", "MonkeyKing", "XinZhao"
        };
        private readonly string[] Tank =
        {
            "Alistar", "Amumu", "Braum", "Chogath", "DrMundo", "Galio", "Garen", "Hecarim", "Malphite", "Maokai", "Nasus", "Nautilus", "Nunu", "Rammus",
            "Sejuani", "Shen", "Singed", "Skarner", "Taric", "Volibear", "Warwick", "Yorick", "Zac"
        };

        private Menu Config;
        private Obj_AI_Hero Player = ObjectManager.Player, SelectedTarget = null;
        public Obj_AI_Hero Target = null;

        public TargetSelector(Menu MainMenu)
        {
            try
            {
                Config = MainMenu;
                var TSMenu = new Menu("Target Selector", "TS");
                {
                    TSMenu.AddItem(new MenuItem("TS_Mode", "Mode").SetValue(new StringList(new[] { "Priority", "Most AD", "Most AP", "Less Attack", "Less Cast", "Low Hp", "Closest", "Near Mouse" })));
                    TSMenu.AddItem(new MenuItem("TS_Range", "Get Target In").SetValue(new Slider(1300, 800, 1600)));
                    TSMenu.AddItem(new MenuItem("TS_Focus", "Forced Target").SetValue(true));
                    TSMenu.AddItem(new MenuItem("TS_Draw", "Draw Target").SetValue(new Circle(false, Color.Red)));
                    TSMenu.AddItem(new MenuItem("TS_AutoPrior", "Auto Arrange Priorities").SetValue(true)).ValueChanged += PriorityChanger;
                    foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
                    {
                        TSMenu.AddItem(new MenuItem("TS_Prior" + Obj.ChampionName, Obj.ChampionName).SetValue(new Slider(TSMenu.Item("TS_AutoPrior").GetValue<bool>() ? (int)GetPriority(Obj.ChampionName) : 1, 1, 5)));
                    }
                    Config.AddSubMenu(TSMenu);
                }
                Game.OnGameUpdate += OnGameUpdate;
                Drawing.OnDraw += OnDraw;
                Game.OnWndProc += OnWndProc;
                Game.PrintChat("<font color = \'{0}'>-></font> <font color = \'{1}'>Target Selector</font>: <font color = \'{2}'>Loaded !</font>", HTMLColor.BlueViolet, HTMLColor.Gold, HTMLColor.Cyan);
            }
            catch
            {
                Game.PrintChat("<font color = \'{0}'>-></font> <font color = \'{1}'>Target Selector</font>: <font color = \'{2}'>Can't Load !</font>", HTMLColor.BlueViolet, HTMLColor.Gold, HTMLColor.Cyan);
            }
        }

        private void PriorityChanger(object sender, OnValueChangeEventArgs e)
        {
            if (!e.GetNewValue<bool>()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy)) Config.SubMenu("TS").Item("TS_Prior" + Obj.ChampionName).SetValue(new Slider((int)GetPriority(Obj.ChampionName), 1, 5));
        }

        private void OnGameUpdate(EventArgs args)
        {
            Target = GetTarget(Config.SubMenu("TS").Item("TS_Range").GetValue<Slider>().Value);
            Orbwalker.ForcedTarget = Config.SubMenu("TS").Item("TS_Focus").GetValue<bool>() ? Target : null;
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead || !Config.SubMenu("TS").Item("TS_Draw").GetValue<Circle>().Active || Target == null) return;
            Render.Circle.DrawCircle(Target.Position, Target.BoundingRadius, Config.SubMenu("TS").Item("TS_Draw").GetValue<Circle>().Color, 7);
        }

        private void OnWndProc(WndEventArgs args)
        {
            if (Player.IsDead || args.Msg != (uint)WindowsMessages.WM_LBUTTONDOWN || MenuGUI.IsChatOpen) return;
            SelectedTarget = ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(200, true, Game.CursorPos)).OrderBy(i => i.Distance(Game.CursorPos)).FirstOrDefault();
        }

        private Obj_AI_Hero GetTarget(float Range)
        {
            if (SelectedTarget.IsValidTarget(Range)) return SelectedTarget;
            var Obj = ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(Range));
            switch (Config.SubMenu("TS").Item("TS_Mode").GetValue<StringList>().SelectedIndex)
            {
                case 0:
                    return Obj.OrderByDescending(i => Player.CalcDamage(i, Damage.DamageType.Physical, 100) / (1 + i.Health) * GetPriority(i.ChampionName, false)).FirstOrDefault();
                case 1:
                    return Obj.OrderByDescending(i => i.BaseAttackDamage + i.FlatPhysicalDamageMod).FirstOrDefault();
                case 2:
                    return Obj.OrderByDescending(i => i.FlatMagicDamageMod).FirstOrDefault();
                case 3:
                    return Obj.OrderByDescending(i => i.Health - Player.CalcDamage(i, Damage.DamageType.Physical, i.Health)).FirstOrDefault();
                case 4:
                    return Obj.OrderByDescending(i => i.Health - Player.CalcDamage(i, Damage.DamageType.Magical, i.Health)).FirstOrDefault();
                case 5:
                    return Obj.OrderBy(i => i.Health).FirstOrDefault();
                case 6:
                    return Obj.OrderBy(i => Player.Distance(i, true)).FirstOrDefault();
                case 7:
                    return Obj.FirstOrDefault(i => i.Distance(Game.CursorPos) < 150);
            }
            return null;
        }

        private float GetPriority(string ChampName, bool IsMenu = true)
        {
            if (IsMenu)
            {
                if (AD.Contains(ChampName)) return 5;
                if (AP.Contains(ChampName)) return 4;
                if (Support.Contains(ChampName)) return 3;
                if (Bruiser.Contains(ChampName)) return 2;
                if (Tank.Contains(ChampName)) return 1;
                return 1;
            }
            else
            {
                switch (Config.SubMenu("TS").Item("TS_Prior" + ChampName).GetValue<Slider>().Value)
                {
                    case 2:
                        return 1.5f;
                    case 3:
                        return 1.75f;
                    case 4:
                        return 2;
                    case 5:
                        return 2.5f;
                }
                return 1;
            }
        }
    }
}
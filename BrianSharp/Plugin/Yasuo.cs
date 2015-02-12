using System;
using System.Drawing;
using System.Linq;
using BrianSharp.Common;
using LeagueSharp;
using LeagueSharp.Common;
using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    internal class Yasuo : Helper
    {
        public Yasuo()
        {
            Q = new Spell(SpellSlot.Q, 550);
            Q2 = new Spell(SpellSlot.Q, 1150);
            W = new Spell(SpellSlot.W, 400);
            E = new Spell(SpellSlot.E, 475);
            R = new Spell(SpellSlot.R, 1300);
            Q.SetSkillshot(0.4f, 20, float.MaxValue, false, SkillshotType.SkillshotLine);
            Q2.SetSkillshot(0.5f, 90, 1500, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.1f, 350, float.MaxValue, false, SkillshotType.SkillshotCircle);

            var champMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var comboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(comboMenu, "Q", "Use Q");
                    AddItem(comboMenu, "E", "Use E");
                    AddItem(comboMenu, "EDmg", "-> Deal Damage");
                    AddItem(comboMenu, "EGap", "-> Gap Closer");
                    AddItem(comboMenu, "EGapRange", "--> If Enemy Not In", 300, 0, 475);
                    AddItem(comboMenu, "R", "Use R");
                    AddItem(comboMenu, "RKill", "-> If Killable");
                    AddItem(comboMenu, "RHpU", "-> If Enemy Hp Under (0 = Off)", 50, 0);
                    AddItem(comboMenu, "RCountA", "-> If Enemy Above (0 = Off)", 2, 0, 5);
                    champMenu.AddSubMenu(comboMenu);
                }
                var harassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(harassMenu, "Q", "Auto Q", "H", KeyBindType.Toggle);
                    AddItem(harassMenu, "Q3", "-> Use Q3");
                    AddItem(harassMenu, "QTower", "-> Under Tower");
                    champMenu.AddSubMenu(harassMenu);
                }
                var clearMenu = new Menu("Clear", "Clear");
                {
                    AddItem(clearMenu, "Q", "Use Q");
                    AddItem(clearMenu, "E", "Use E");
                    AddItem(clearMenu, "ETower", "-> Under Tower");
                    AddItem(clearMenu, "Item", "Use Tiamat/Hydra Item");
                    champMenu.AddSubMenu(clearMenu);
                }
                var lastHitMenu = new Menu("Last Hit", "LastHit");
                {
                    AddItem(lastHitMenu, "Q", "Use Q");
                    AddItem(lastHitMenu, "Q3", "-> Use Q3");
                    AddItem(lastHitMenu, "E", "Use E");
                    AddItem(lastHitMenu, "ETower", "-> Under Tower");
                    champMenu.AddSubMenu(lastHitMenu);
                }
                var fleeMenu = new Menu("Flee", "Flee");
                {
                    AddItem(fleeMenu, "E", "Use E");
                    AddItem(fleeMenu, "EStackQ", "-> Stack Q");
                    champMenu.AddSubMenu(fleeMenu);
                }
                var miscMenu = new Menu("Misc", "Misc");
                {
                    var killStealMenu = new Menu("Kill Steal", "KillSteal");
                    {
                        AddItem(killStealMenu, "Q", "Use Q");
                        AddItem(killStealMenu, "E", "Use E");
                        AddItem(killStealMenu, "R", "Use R");
                        AddItem(killStealMenu, "Ignite", "Use Ignite");
                        miscMenu.AddSubMenu(killStealMenu);
                    }
                    var interruptMenu = new Menu("Interrupt", "Interrupt");
                    {
                        AddItem(interruptMenu, "Q", "Use Q3");
                        foreach (var spell in
                            Interrupter.Spells.FindAll(
                                i => HeroManager.Enemies.Any(a => i.ChampionName == a.ChampionName)))
                        {
                            AddItem(
                                interruptMenu, spell.ChampionName + "_" + spell.Slot,
                                "-> Skill " + spell.Slot + " Of " + spell.ChampionName);
                        }
                        miscMenu.AddSubMenu(interruptMenu);
                    }
                    champMenu.AddSubMenu(miscMenu);
                }
                var drawMenu = new Menu("Draw", "Draw");
                {
                    AddItem(drawMenu, "Q", "Q Range", false);
                    AddItem(drawMenu, "E", "E Range", false);
                    AddItem(drawMenu, "R", "R Range", false);
                    champMenu.AddSubMenu(drawMenu);
                }
                MainMenu.AddSubMenu(champMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
        }

        private bool HaveQ3
        {
            get { return Player.HasBuff("YasuoQ3W"); }
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling())
            {
                return;
            }
            switch (Orbwalk.CurrentMode)
            {
                case Orbwalker.Mode.Combo:
                    //Fight();
                    break;
                case Orbwalker.Mode.Clear:
                    //Clear();
                    break;
                case Orbwalker.Mode.LastHit:
                    //LastHit();
                    break;
                //case Orbwalker.Mode.Flee:
                //    Flee();
                //    break;
            }
            AutoQ();
            //KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead)
            {
                return;
            }
            if (GetValue<bool>("Draw", "Q") && Q.Level > 0)
            {
                Render.Circle.DrawCircle(
                    Player.Position, (HaveQ3 ? Q2 : Q).Range, Q.IsReady() ? Color.Green : Color.Red);
            }
            if (GetValue<bool>("Draw", "E") && E.Level > 0)
            {
                Render.Circle.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            }
            if (GetValue<bool>("Draw", "R") && R.Level > 0)
            {
                Render.Circle.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
            }
        }

        private void OnPossibleToInterrupt(Obj_AI_Hero unit, InterruptableSpell spell)
        {
            if (Player.IsDead || !GetValue<bool>("Interrupt", "Q") ||
                !GetValue<bool>("Interrupt", unit.ChampionName + "_" + spell.Slot) || !Q.IsReady() || !HaveQ3)
            {
                return;
            }
            if (E.IsInRange(unit) && E.CastOnUnit(unit, PacketCast) && Q.Cast(PacketCast))
            {
                return;
            }
            Q2.CastIfHitchanceEquals(unit, HitChance.High, PacketCast);
        }

        private void AutoQ()
        {
            if (!GetValue<KeyBind>("Harass", "Q").Active || !Q.IsReady() || (HaveQ3 && !GetValue<bool>("Harass", "Q3")))
            {
                return;
            }
            var target = (HaveQ3 ? Q2 : Q).GetTarget();
            if (target == null || (GetValue<bool>("Harass", "QTower") && UnderTower(target)))
            {
                return;
            }
            (HaveQ3 ? Q2 : Q).CastIfHitchanceEquals(target, HitChance.High, PacketCast);
        }

        private bool UnderTower(Obj_AI_Base target)
        {
            return ObjectManager.Get<Obj_AI_Turret>().Any(i => i.IsEnemy && !i.IsDead && i.Distance(target) <= 950);
        }
    }
}
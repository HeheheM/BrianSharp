using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    class Aatrox : Common.Helper
    {
        public Aatrox()
        {
            Q = new Spell(SpellSlot.Q, 720);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 1000, TargetSelector.DamageType.Magical);
            R = new Spell(SpellSlot.R, 550, TargetSelector.DamageType.Magical);
            Q.SetSkillshot(0.5f, 285, float.MaxValue, false, SkillshotType.SkillshotCircle);
            E.SetSkillshot(0.5f, 150, 1200, true, SkillshotType.SkillshotLine);
            R.SetSkillshot(0.5f, 550, 800, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(ComboMenu, "Q", "Use Q");
                    AddItem(ComboMenu, "W", "Use W");
                    AddItem(ComboMenu, "WHpU", "-> Switch To Heal If Hp Under", 50);
                    AddItem(ComboMenu, "E", "Use E");
                    AddItem(ComboMenu, "R", "Use R");
                    AddItem(ComboMenu, "RHpU", "-> If Enemy Hp Under", 60);
                    AddItem(ComboMenu, "RCountA", "-> If Enemy Above", 2, 1, 4);
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(HarassMenu, "AutoE", "Use E", "H", KeyBindType.Toggle);
                    AddItem(HarassMenu, "AutoEHpA", "-> If Hp Above", 50);
                    AddItem(HarassMenu, "Q", "Use Q");
                    AddItem(HarassMenu, "QHpA", "-> If Hp Above", 20);
                    AddItem(HarassMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    AddItem(ClearMenu, "Q", "Use Q");
                    AddItem(ClearMenu, "W", "Use W");
                    AddItem(ClearMenu, "WPriority", "-> Priority Heal");
                    AddItem(ClearMenu, "WHpU", "-> Switch To Heal If Hp Under", 50);
                    AddItem(ClearMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var FleeMenu = new Menu("Flee", "Flee");
                {
                    AddItem(FleeMenu, "Q", "Use Q");
                    AddItem(FleeMenu, "E", "Use E To Slow Enemy");
                    ChampMenu.AddSubMenu(FleeMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    AddItem(MiscMenu, "Ks", "Kill Steal");
                    AddItem(MiscMenu, "KsQ", "-> Use Q");
                    AddItem(MiscMenu, "KsE", "-> Use E");
                    AddItem(MiscMenu, "KsIgnite", "-> Use Ignite");
                    AddItem(MiscMenu, "QAntiGap", "Use Q To Anti Gap Closer");
                    AddItem(MiscMenu, "QInterrupt", "Use Q To Interrupt");
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    AddItem(DrawMenu, "Q", "Q Range", false);
                    AddItem(DrawMenu, "E", "E Range", false);
                    AddItem(DrawMenu, "R", "R Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                MainMenu.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee) Flee();
            if (GetValue<KeyBind>("Harass", "AutoE").Active) AutoE();
            if (GetValue<bool>("Misc", "Ks")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (GetValue<bool>("Draw", "Q") && Q.Level > 0) Render.Circle.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "E") && E.Level > 0) Render.Circle.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "R") && R.Level > 0) Render.Circle.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (Player.IsDead || !GetValue<bool>("Misc", "QAntiGap") || !Q.IsReady() || !Orbwalk.InAutoAttackRange(gapcloser.Sender)) return;
            if (Q.Cast(gapcloser.Sender, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (Player.IsDead || !GetValue<bool>("Misc", "QInterrupt") || !Q.CanCast(unit)) return;
            if (Q.Cast(unit, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
        }

        private void NormalCombo(string Mode)
        {
            if (GetValue<bool>(Mode, "Q") && Q.IsReady())
            {
                var Target = Q.GetTarget(Q.Width / 2);
                if (Target != null && (Mode == "Combo" || (Mode == "Harass" && Player.HealthPercentage() >= GetValue<Slider>(Mode, "QHpA").Value)) && Q.Cast(Target, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
            }
            if (GetValue<bool>(Mode, "E") && E.IsReady())
            {
                var Target = E.GetTarget();
                if (Target != null && E.Cast(Target, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
            }
            if (Mode == "Combo")
            {
                if (GetValue<bool>(Mode, "W") && W.IsReady())
                {
                    if (TargetSelector.GetTarget(Orbwalk.GetAutoAttackRange(), TargetSelector.DamageType.Physical) != null)
                    {
                        if (Player.HealthPercentage() >= GetValue<Slider>("Clear", "WHpU").Value)
                        {
                            if (!Player.HasBuff("AatroxWPower") && W.Cast(PacketCast)) return;
                        }
                        else if (Player.HasBuff("AatroxWPower") && W.Cast(PacketCast)) return;
                    }
                }
                if (GetValue<bool>(Mode, "R") && R.IsReady())
                {
                    if (Player.CountEnemysInRange(R.Range) == 1)
                    {
                        var Target = R.GetTarget();
                        if (Target != null && Target.HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value && R.Cast(PacketCast)) return;
                    }
                    else
                    {
                        var Target = ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(R.Range));
                        if (((Target.Count() >= 2 && Target.Count(i => CanKill(i, R)) >= 1) || Target.Count() >= GetValue<Slider>(Mode, "RCountA").Value || Target.Count(i => i.HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value) >= 2) && R.Cast(PacketCast)) return;
                    }
                }
            }
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            if (minionObj.Count == 0) return;
            if (GetValue<bool>("Clear", "Q") && Q.IsReady())
            {
                var Pos = Q.GetCircularFarmLocation(minionObj.Where(i => Player.Distance(i, true) <= Q.RangeSqr + Q.WidthSqr / 2).ToList(), Q.Width - 30);
                if (Pos.MinionsHit > 0 && Q.Cast(Pos.Position, PacketCast)) return;
            }
            if ((!GetValue<bool>("Clear", "Q") || (GetValue<bool>("Clear", "Q") && !Q.IsReady())) && GetValue<bool>("Clear", "E") && E.IsReady())
            {
                var Pos = E.GetLineFarmLocation(minionObj);
                if (Pos.MinionsHit > 0 && E.Cast(Pos.Position, PacketCast)) return;
            }
            if (GetValue<bool>("Clear", "W") && W.IsReady())
            {
                if (Player.HealthPercentage() >= (GetValue<bool>("Clear", "WPriority") ? 85 : GetValue<Slider>("Clear", "WHpU").Value))
                {
                    if (!Player.HasBuff("AatroxWPower") && W.Cast(PacketCast)) return;
                }
                else if (Player.HasBuff("AatroxWPower") && W.Cast(PacketCast)) return;
            }
        }

        private void Flee()
        {
            if (GetValue<bool>("Flee", "Q") && Q.IsReady() && Q.Cast(Game.CursorPos, PacketCast)) return;
            if (GetValue<bool>("Flee", "E") && E.IsReady())
            {
                var Target = E.GetTarget();
                if (Target != null && E.Cast(Target, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
            }
        }

        private void AutoE()
        {
            if (!E.IsReady() || Player.HealthPercentage() < GetValue<Slider>("Harass", "AutoEHpA").Value) return;
            var Target = E.GetTarget();
            if (Target != null && E.Cast(Target, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
        }

        private void KillSteal()
        {
            if (GetValue<bool>("Misc", "KsIgnite") && Ignite.IsReady())
            {
                var Target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.True);
                if (Target != null && CastIgnite(Target)) return;
            }
            if (GetValue<bool>("Misc", "KsQ") && Q.IsReady())
            {
                var Target = Q.GetTarget(Q.Width / 2);
                if (Target != null && CanKill(Target, Q) && Q.Cast(Target, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
            }
            if (GetValue<bool>("Misc", "KsE") && E.IsReady())
            {
                var Target = E.GetTarget();
                if (Target != null && CanKill(Target, E) && E.Cast(Target, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
            }
        }
    }
}
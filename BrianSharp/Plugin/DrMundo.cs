using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    class DrMundo : Common.Helper
    {
        public DrMundo()
        {
            Q = new Spell(SpellSlot.Q, 1050);
            W = new Spell(SpellSlot.W, 325);
            E = new Spell(SpellSlot.E);
            R = new Spell(SpellSlot.R);
            Q.SetSkillshot(0.25f, 60, 2000, true, SkillshotType.SkillshotLine);

            var ChampMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(ComboMenu, "Q", "Use Q");
                    AddItem(ComboMenu, "QCol", "-> Smite Collision");
                    AddItem(ComboMenu, "W", "Use W");
                    AddItem(ComboMenu, "WHpA", "-> If Hp Above", 20);
                    AddItem(ComboMenu, "E", "Use E");
                    AddItem(ComboMenu, "R", "Use R");
                    AddItem(ComboMenu, "RHpU", "-> If Hp Under", 50);
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(HarassMenu, "AutoQ", "Use Q", "H", KeyBindType.Toggle);
                    AddItem(HarassMenu, "AutoQHpA", "-> If Hp Above", 30);
                    AddItem(HarassMenu, "Q", "Use Q");
                    AddItem(HarassMenu, "W", "Use W");
                    AddItem(HarassMenu, "WHpA", "-> If Hp Above", 20);
                    AddItem(HarassMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    var SmiteMob = new Menu("Smite Mob If Killable", "SmiteMob");
                    {
                        AddItem(SmiteMob, "Smite", "Use Smite");
                        AddItem(SmiteMob, "Baron", "-> Baron Nashor");
                        AddItem(SmiteMob, "Dragon", "-> Dragon");
                        AddItem(SmiteMob, "Red", "-> Red Brambleback");
                        AddItem(SmiteMob, "Blue", "-> Blue Sentinel");
                        AddItem(SmiteMob, "Krug", "-> Ancient Krug");
                        AddItem(SmiteMob, "Gromp", "-> Gromp");
                        AddItem(SmiteMob, "Raptor", "-> Crimson Raptor");
                        AddItem(SmiteMob, "Wolf", "-> Greater Murk Wolf");
                        ClearMenu.AddSubMenu(SmiteMob);
                    }
                    AddItem(ClearMenu, "Q", "Use Q");
                    AddItem(ClearMenu, "W", "Use W");
                    AddItem(ClearMenu, "WHpA", "-> If Hp Above", 20);
                    AddItem(ClearMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    var KillStealMenu = new Menu("Kill Steal", "KillSteal");
                    {
                        AddItem(KillStealMenu, "Q", "Use Q");
                        AddItem(KillStealMenu, "Ignite", "Use Ignite");
                        MiscMenu.AddSubMenu(KillStealMenu);
                    }
                    AddItem(MiscMenu, "QLastHit", "Use Q To Last Hit");
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    AddItem(DrawMenu, "Q", "Q Range", false);
                    AddItem(DrawMenu, "W", "W Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                MainMenu.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Orbwalk.BeforeAttack += BeforeAttack;
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit) LastHit();
            if (GetValue<KeyBind>("Harass", "AutoQ").Active) AutoQ();
            KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (GetValue<bool>("Draw", "Q") && Q.Level > 0) Render.Circle.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "W") && W.Level > 0) Render.Circle.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
        }

        private void BeforeAttack(Orbwalk.BeforeAttackEventArgs Args)
        {
            if ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass) && GetValue<bool>(Orbwalk.CurrentMode.ToString(), "E") && Args.Target is Obj_AI_Hero && E.Cast(PacketCast))
            {
                return;
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear && GetValue<bool>("Clear", "E") && Args.Target is Obj_AI_Minion && E.Cast(PacketCast)) return;
        }

        private void NormalCombo(string Mode)
        {
            if (GetValue<bool>(Mode, "W") && W.IsReady() && Player.HasBuff("BurningAgony") && W.GetTarget(175) == null && W.Cast(PacketCast)) return;
            if (GetValue<bool>(Mode, "Q") && Q.IsReady())
            {
                var State = Q.CastOnBestTarget(0, PacketCast);
                if (State == Spell.CastStates.SuccessfullyCasted)
                {
                    return;
                }
                else if (Mode == "Combo" && State == Spell.CastStates.Collision && GetValue<bool>(Mode, "QCol"))
                {
                    var Pred = Q.GetPrediction(Q.GetTarget());
                    if (Pred.CollisionObjects.FindAll(i => i.IsMinion).Count == 1 && CastSmite(Pred.CollisionObjects.First()) && Q.Cast(Pred.CastPosition, PacketCast)) return;
                }
            }
            if (GetValue<bool>(Mode, "W") && W.IsReady())
            {
                if (Player.HealthPercentage() >= GetValue<Slider>(Mode, "WHpA").Value)
                {
                    if (W.GetTarget(60) != null)
                    {
                        if (!Player.HasBuff("BurningAgony") && W.Cast(PacketCast)) return;
                    }
                    else if (Player.HasBuff("BurningAgony") && W.Cast(PacketCast)) return;
                }
                else if (Player.HasBuff("BurningAgony") && W.Cast(PacketCast)) return;
            }
            if (Mode == "Combo" && GetValue<bool>(Mode, "R") && Player.HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value && !Player.InFountain() && Q.GetTarget() != null && R.Cast(PacketCast)) return;
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            if (minionObj.Count == 0)
            {
                if (GetValue<bool>("Clear", "W") && W.IsReady() && Player.HasBuff("BurningAgony")) W.Cast(PacketCast);
                return;
            }
            if (GetValue<bool>("Clear", "Q") && Q.IsReady())
            {
                var Obj = minionObj.Find(i => CanKill(i, Q));
                if (Obj == null) Obj = minionObj.Find(i => i.MaxHealth >= 1200);
                if (Obj != null && Q.CastIfHitchanceEquals(Obj, HitChance.Medium, PacketCast)) return;
            }
            if (GetValue<bool>("Clear", "W") && W.IsReady())
            {
                if (Player.HealthPercentage() >= GetValue<Slider>("Clear", "WHpA").Value)
                {
                    if (minionObj.Count(i => W.IsInRange(i, W.Range + 60)) > 1 || minionObj.Count(i => i.MaxHealth >= 1200 && W.IsInRange(i, W.Range + 60)) > 0)
                    {
                        if (!Player.HasBuff("BurningAgony") && W.Cast(PacketCast)) return;
                    }
                    else if (Player.HasBuff("BurningAgony") && W.Cast(PacketCast)) return;
                }
                else if (Player.HasBuff("BurningAgony") && W.Cast(PacketCast)) return;
            }
            if (GetValue<bool>("SmiteMob", "Smite") && Smite.IsReady())
            {
                var Obj = minionObj.Find(i => i.Team == GameObjectTeam.Neutral && CanSmiteMob(i.Name));
                if (Obj != null && CastSmite(Obj)) return;
            }
        }

        private void LastHit()
        {
            if (!GetValue<bool>("Misc", "QLastHit") || !Q.IsReady()) return;
            var minionObj = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth).FindAll(i => CanKill(i, Q));
            if (minionObj.Count == 0 || Q.CastIfHitchanceEquals(minionObj.First(), HitChance.High, PacketCast)) return;
        }

        private void AutoQ()
        {
            if (Player.HealthPercentage() < GetValue<Slider>("Harass", "AutoQHpA").Value || Q.CastOnBestTarget(0, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
        }

        private void KillSteal()
        {
            if (GetValue<bool>("KillSteal", "Ignite") && Ignite.IsReady())
            {
                var Target = TargetSelector.GetTarget(600, TargetSelector.DamageType.True);
                if (Target != null && CastIgnite(Target)) return;
            }
            if (GetValue<bool>("KillSteal", "Q") && Q.IsReady())
            {
                var Target = Q.GetTarget();
                if (Target != null && CanKill(Target, Q) && Q.CastIfHitchanceEquals(Target, HitChance.High, PacketCast)) return;
            }
        }
    }
}
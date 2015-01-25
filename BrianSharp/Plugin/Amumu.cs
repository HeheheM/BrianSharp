using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    class Amumu : Common.Helper
    {
        public Amumu()
        {
            Q = new Spell(SpellSlot.Q, 1100);
            W = new Spell(SpellSlot.W, 300);
            E = new Spell(SpellSlot.E, 350);
            R = new Spell(SpellSlot.R, 550);
            Q.SetSkillshot(0.25f, 90, 2000, true, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.5f, 350, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.25f, 550, float.MaxValue, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(ComboMenu, "Q", "Use Q");
                    AddItem(ComboMenu, "QCol", "-> Smite Collision");
                    AddItem(ComboMenu, "W", "Use W");
                    AddItem(ComboMenu, "WMpA", "-> If Mp Above", 20);
                    AddItem(ComboMenu, "E", "Use E");
                    AddItem(ComboMenu, "R", "Use R");
                    AddItem(ComboMenu, "RHpU", "-> If Enemy Hp Under", 60);
                    AddItem(ComboMenu, "RCountA", "-> If Enemy Above", 2, 1, 5);
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(HarassMenu, "W", "Use W");
                    AddItem(HarassMenu, "WMpA", "-> If Mp Above", 20);
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
                    AddItem(ClearMenu, "WMpA", "-> If Mp Above", 20);
                    AddItem(ClearMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    var KillStealMenu = new Menu("Kill Steal", "KillSteal");
                    {
                        AddItem(KillStealMenu, "E", "Use E");
                        AddItem(KillStealMenu, "R", "Use R");
                        AddItem(KillStealMenu, "Ignite", "Use Ignite");
                        MiscMenu.AddSubMenu(KillStealMenu);
                    }
                    var AntiGapMenu = new Menu("Anti Gap Closer", "AntiGap");
                    {
                        AddItem(AntiGapMenu, "Q", "Use Q");
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
                        {
                            foreach (var Spell in AntiGapcloser.Spells.Where(i => i.ChampionName == Obj.ChampionName)) AddItem(AntiGapMenu, Obj.ChampionName + "_" + Spell.Slot.ToString(), "-> Skill " + Spell.Slot.ToString() + " Of " + Obj.ChampionName);
                        }
                        MiscMenu.AddSubMenu(AntiGapMenu);
                    }
                    var InterruptMenu = new Menu("Interrupt", "Interrupt");
                    {
                        AddItem(InterruptMenu, "Q", "Use Q");
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
                        {
                            foreach (var Spell in Interrupter.Spells.Where(i => i.ChampionName == Obj.ChampionName)) AddItem(InterruptMenu, Obj.ChampionName + "_" + Spell.Slot.ToString(), "-> Skill " + Spell.Slot.ToString() + " Of " + Obj.ChampionName);
                        }
                        MiscMenu.AddSubMenu(InterruptMenu);
                    }
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    AddItem(DrawMenu, "Q", "Q Range", false);
                    AddItem(DrawMenu, "W", "W Range", false);
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear) LaneJungClear();
            KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (GetValue<bool>("Draw", "Q") && Q.Level > 0) Render.Circle.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "W") && W.Level > 0) Render.Circle.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "E") && E.Level > 0) Render.Circle.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "R") && R.Level > 0) Render.Circle.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (Player.IsDead || !GetValue<bool>("AntiGap", "Q") || !GetValue<bool>("AntiGap", gapcloser.Sender.ChampionName + "_" + gapcloser.Slot.ToString()) || !Q.IsReady() || !Orbwalk.InAutoAttackRange(gapcloser.Sender)) return;
            if (Q.CastIfHitchanceEquals(gapcloser.Sender, HitChance.High, PacketCast)) return;
        }

        private void OnPossibleToInterrupt(Obj_AI_Hero unit, InterruptableSpell spell)
        {
            if (Player.IsDead || !GetValue<bool>("Interrupt", "Q") || !GetValue<bool>("Interrupt", unit.ChampionName + "_" + spell.Slot.ToString()) || !Q.CanCast(unit)) return;
            if (Q.CastIfHitchanceEquals(unit, HitChance.High, PacketCast)) return;
        }

        private void NormalCombo(string Mode)
        {
            if (GetValue<bool>(Mode, "W") && W.IsReady() && Player.HasBuff("AuraofDespair") && W.GetTarget(200) == null && W.Cast(PacketCast)) return;
            if (Mode == "Combo")
            {
                if (GetValue<bool>(Mode, "R") && R.IsReady())
                {
                    var Target = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => i.IsValidTarget(R.Range));
                    if (((Target.Count > 1 && Target.Count(i => CanKill(i, R)) > 0) || Target.Count >= GetValue<Slider>(Mode, "RCountA").Value || (Target.Count > 1 && Target.Count(i => i.HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value) > 0)) && R.Cast(PacketCast)) return;
                }
                if (GetValue<bool>(Mode, "Q") && Q.IsReady())
                {
                    if (GetValue<bool>(Mode, "R") && R.IsReady())
                    {
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().FindAll(i => !(i is Obj_AI_Turret) && i.IsValidTarget(Q.Range) && Q.GetPrediction(i).Hitchance >= HitChance.High).OrderByDescending(i => i.CountEnemiesInRange(R.Range)))
                        {
                            var Sub = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => i.IsValidTarget(R.Range - 20, true, Obj.ServerPosition));
                            if (Sub.Count > 0 && ((Sub.Count > 1 && Sub.Count(i => CanKill(i, R)) > 0) || Sub.Count >= GetValue<Slider>(Mode, "RCountA").Value || (Sub.Count > 1 && Sub.Count(i => i.HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value) > 0)) && Q.CastIfHitchanceEquals(Obj, HitChance.High, PacketCast)) return;
                        }
                    }
                    var Target = Q.GetTarget();
                    if (Target != null && !Orbwalk.InAutoAttackRange(Target))
                    {
                        var State = Q.Cast(Target, PacketCast);
                        if (State == Spell.CastStates.SuccessfullyCasted)
                        {
                            return;
                        }
                        else if (State == Spell.CastStates.Collision && GetValue<bool>(Mode, "QCol"))
                        {
                            var Pred = Q.GetPrediction(Target);
                            if (Pred.CollisionObjects.FindAll(i => i.IsMinion).Count == 1 && CastSmite(Pred.CollisionObjects.First()) && Q.Cast(Pred.CastPosition, PacketCast)) return;
                        }
                    }
                }
            }
            if (GetValue<bool>(Mode, "E") && E.IsReady() && E.GetTarget() != null && E.Cast(PacketCast)) return;
            if (GetValue<bool>(Mode, "W") && W.IsReady())
            {
                if (Player.ManaPercentage() >= GetValue<Slider>(Mode, "WMpA").Value)
                {
                    if (W.GetTarget(60) != null)
                    {
                        if (!Player.HasBuff("AuraofDespair") && W.Cast(PacketCast)) return;
                    }
                    else if (Player.HasBuff("AuraofDespair") && W.Cast(PacketCast)) return;
                }
                else if (Player.HasBuff("AuraofDespair") && W.Cast(PacketCast)) return;
            }
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            if (minionObj.Count == 0)
            {
                if (GetValue<bool>("Clear", "W") && W.IsReady() && Player.HasBuff("AuraofDespair")) W.Cast(PacketCast);
                return;
            }
            if (GetValue<bool>("Clear", "E") && E.IsReady() && minionObj.Count(i => E.IsInRange(i)) > 0 && E.Cast(PacketCast)) return;
            if (GetValue<bool>("Clear", "W") && W.IsReady())
            {
                if (Player.ManaPercentage() >= GetValue<Slider>("Clear", "WMpA").Value)
                {
                    if (minionObj.Count(i => W.IsInRange(i, W.Range + 60)) > 1 || minionObj.Count(i => i.MaxHealth >= 1200 && W.IsInRange(i, W.Range + 60)) > 0)
                    {
                        if (!Player.HasBuff("AuraofDespair") && W.Cast(PacketCast)) return;
                    }
                    else if (Player.HasBuff("AuraofDespair") && W.Cast(PacketCast)) return;
                }
                else if (Player.HasBuff("AuraofDespair") && W.Cast(PacketCast)) return;
            }
            if (GetValue<bool>("Clear", "Q") && Q.IsReady())
            {
                var Obj = minionObj.Find(i => CanKill(i, Q));
                if (Obj == null) Obj = minionObj.Find(i => !Orbwalk.InAutoAttackRange(i));
                if (Obj != null && Q.CastIfHitchanceEquals(Obj, HitChance.Medium, PacketCast)) return;
            }
            if (GetValue<bool>("SmiteMob", "Smite") && Smite.IsReady())
            {
                var Obj = minionObj.Find(i => i.Team == GameObjectTeam.Neutral && CanSmiteMob(i.Name));
                if (Obj != null && CastSmite(Obj)) return;
            }
        }

        private void KillSteal()
        {
            if (GetValue<bool>("KillSteal", "Ignite") && Ignite.IsReady())
            {
                var Target = TargetSelector.GetTarget(600, TargetSelector.DamageType.True);
                if (Target != null && CastIgnite(Target)) return;
            }
            if (GetValue<bool>("KillSteal", "E") && E.IsReady())
            {
                var Target = E.GetTarget();
                if (Target != null && CanKill(Target, E) && E.Cast(PacketCast)) return;
            }
            if (GetValue<bool>("KillSteal", "R") && R.IsReady())
            {
                var Target = R.GetTarget();
                if (Target != null && CanKill(Target, R) && R.Cast(PacketCast)) return;
            }
        }
    }
}
using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    class Maokai : Common.Helper
    {
        public Maokai()
        {
            Q = new Spell(SpellSlot.Q, 630);
            W = new Spell(SpellSlot.W, 525);
            E = new Spell(SpellSlot.E, 1115);
            R = new Spell(SpellSlot.R, 478);
            Q.SetSkillshot(0.3333f, 110, 1100, false, SkillshotType.SkillshotLine);
            W.SetTargetted(0.5f, 1000);
            E.SetSkillshot(0.25f, 225, 1750, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.25f, 478, float.MaxValue, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(ComboMenu, "Q", "Use Q");
                    AddItem(ComboMenu, "W", "Use W");
                    AddItem(ComboMenu, "E", "Use E");
                    AddItem(ComboMenu, "R", "Use R");
                    AddItem(ComboMenu, "RHpU", "-> If Enemy Hp Under", 60);
                    AddItem(ComboMenu, "RCountA", "-> If Enemy Above", 2, 1, 5);
                    AddItem(ComboMenu, "RKill", "-> Cancel If Killable");
                    AddItem(ComboMenu, "RMpU", "-> Cancel If Mp Under", 20);
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(HarassMenu, "AutoQ", "Use Q", "H", KeyBindType.Toggle);
                    AddItem(HarassMenu, "AutoQMpA", "-> If Mp Above", 50);
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
                    AddItem(ClearMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var FleeMenu = new Menu("Flee", "Flee");
                {
                    AddItem(FleeMenu, "W", "Use W");
                    AddItem(FleeMenu, "Q", "Use Q To Slow Enemy");
                    ChampMenu.AddSubMenu(FleeMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    var KillStealMenu = new Menu("Kill Steal", "KillSteal");
                    {
                        AddItem(KillStealMenu, "Q", "Use Q");
                        AddItem(KillStealMenu, "W", "Use W");
                        AddItem(KillStealMenu, "Ignite", "Use Ignite");
                        MiscMenu.AddSubMenu(KillStealMenu);
                    }
                    var AntiGapMenu = new Menu("Anti Gap Closer", "AntiGap");
                    {
                        AddItem(AntiGapMenu, "Q", "Use Q");
                        AddItem(AntiGapMenu, "QSlow", "-> Slow If Cant Knockback (Skillshot)");
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
                    AddItem(MiscMenu, "Gank", "Gank", "Z");
                    AddItem(MiscMenu, "WTower", "Use W If Enemy Under Tower");
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee) Flee();
            if (GetValue<KeyBind>("Misc", "Gank").Active) NormalCombo("Gank");
            if (GetValue<KeyBind>("Harass", "AutoQ").Active) AutoQ();
            KillSteal();
            if (GetValue<bool>("Misc", "WTower")) AutoWUnderTower();
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
            if (Player.IsDead || !GetValue<bool>("AntiGap", "Q") || !GetValue<bool>("AntiGap", gapcloser.Sender.ChampionName + "_" + gapcloser.Slot.ToString()) || !Q.CanCast(gapcloser.Sender)) return;
            if (Player.Distance(gapcloser.Sender, true) <= Math.Pow(100, 2) && Q.Cast(gapcloser.Sender.ServerPosition, PacketCast))
            {
                return;
            }
            else if (GetValue<bool>("AntiGap", "QSlow") && gapcloser.SkillType == GapcloserType.Skillshot && Player.Distance(gapcloser.End) > 100 && Q.CastIfHitchanceEquals(gapcloser.Sender, HitChance.High, PacketCast)) return;
        }

        private void OnPossibleToInterrupt(Obj_AI_Hero unit, InterruptableSpell spell)
        {
            if (Player.IsDead || !GetValue<bool>("Interrupt", "Q") || !GetValue<bool>("Interrupt", unit.ChampionName + "_" + spell.Slot.ToString()) || !Q.IsReady()) return;
            if (Player.Distance(unit, true) > Math.Pow(100, 2) && W.CanCast(unit) && Player.Mana >= Q.Instance.ManaCost + W.Instance.ManaCost && W.CastOnUnit(unit, PacketCast)) return;
            if (Player.Distance(unit, true) <= Math.Pow(100, 2) && Q.Cast(unit.ServerPosition, PacketCast)) return;
        }

        private void NormalCombo(string Mode)
        {
            if (Mode == "Combo" && GetValue<bool>(Mode, "R") && R.IsReady())
            {
                var Target = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => i.IsValidTarget(R.Range));
                if (!Player.HasBuff("MaokaiDrain"))
                {
                    var RCount = GetValue<Slider>(Mode, "RCountA").Value;
                    if (Player.ManaPercentage() >= GetValue<Slider>(Mode, "RMpU").Value && ((RCount > 1 && (Target.Count >= RCount || (Target.Count > 1 && Target.Count(i => i.HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value) > 0) || (Player.CountEnemiesInRange(R.Range + 100) == 1 && R.GetTarget() != null && R.GetTarget().HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value))) || (RCount == 1 && R.GetTarget() != null && R.GetTarget().HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value)) && R.Cast(PacketCast)) return;
                }
                else if (((GetValue<bool>(Mode, "RKill") && ((Player.CountEnemiesInRange(R.Range + 50) == 1 && R.GetTarget() != null && CanKill(Target.First(), R, GetRDmg(Target.First()))) || (Target.Count > 1 && Target.Count(i => CanKill(i, R, GetRDmg(i))) > 0))) || Player.ManaPercentage() < GetValue<Slider>(Mode, "RMpU").Value) && R.Cast(PacketCast)) return;
            }
            if (Mode == "Gank")
            {
                var Target = W.GetTarget(100);
                CustomOrbwalk(Target);
                if (Target != null && W.IsReady())
                {
                    if (E.IsReady())
                    {
                        E.Speed = 1750 - Player.Distance(Target.ServerPosition);
                        if (E.CastIfWillHit(Target, -1, PacketCast))
                        {
                            E.Speed = 1750;
                            return;
                        }
                    }
                    if (W.CastOnUnit(Target, PacketCast))
                    {
                        Utility.DelayAction.Add((int)(W.Delay * 1000 + Player.Distance(Target.ServerPosition) / W.Speed - 100), () => Q.Cast(Target.ServerPosition, PacketCast));
                        return;
                    }
                }
            }
            else
            {
                if (GetValue<bool>(Mode, "E") && E.IsReady())
                {
                    var Target = E.GetTarget();
                    if (Target != null)
                    {
                        E.Speed = 1750 - Player.Distance(Target.ServerPosition);
                        if (E.CastIfWillHit(Target, -1, PacketCast))
                        {
                            E.Speed = 1750;
                            return;
                        }
                    }
                }
                if (GetValue<bool>(Mode, "W") && (Mode == "Combo" || (Mode == "Harass" && Player.HealthPercentage() >= GetValue<Slider>(Mode, "WHpA").Value)) && W.CastOnBestTarget(0, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
                if (GetValue<bool>(Mode, "Q") && Q.CastOnBestTarget(0, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
            }
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            if (minionObj.Count == 0) return;
            if (GetValue<bool>("Clear", "E") && E.IsReady() && (minionObj.Count > 2 || minionObj.Any(i => i.MaxHealth >= 1200)))
            {
                var Pos = E.GetCircularFarmLocation(minionObj);
                if (Pos.MinionsHit > 0 && Pos.Position.IsValid() && E.Cast(Pos.Position, PacketCast)) return;
            }
            if (GetValue<bool>("Clear", "Q") && Q.IsReady())
            {
                var Pos = Q.GetLineFarmLocation(minionObj.FindAll(i => Q.IsInRange(i)));
                if (Pos.MinionsHit > 0 && Pos.Position.IsValid() && Q.Cast(Pos.Position, PacketCast)) return;
            }
            if (GetValue<bool>("Clear", "W") && W.IsReady())
            {
                var Obj = minionObj.Find(i => W.IsInRange(i) && i.MaxHealth >= 1200);
                if (Obj == null && minionObj.Count(i => Player.Distance(i, true) <= Math.Pow(Orbwalk.GetAutoAttackRange(Player, i) + 40, 2)) == 0) Obj = minionObj.FindAll(i => W.IsInRange(i)).MinOrDefault(i => i.Health);
                if (Obj != null && W.CastOnUnit(Obj, PacketCast)) return;
            }
            if (GetValue<bool>("SmiteMob", "Smite") && Smite.IsReady())
            {
                var Obj = minionObj.Find(i => i.Team == GameObjectTeam.Neutral && CanSmiteMob(i.Name));
                if (Obj != null && CastSmite(Obj)) return;
            }
        }

        private void Flee()
        {
            if (GetValue<bool>("Flee", "W") && W.IsReady())
            {
                var Obj = ObjectManager.Get<Obj_AI_Base>().FindAll(i => !(i is Obj_AI_Turret) && i.IsValidTarget(W.Range + i.BoundingRadius) && i.Distance(Game.CursorPos) < 200).MinOrDefault(i => i.Distance(Game.CursorPos));
                if (Obj != null && W.CastOnUnit(Obj, PacketCast)) return;
            }
            if (GetValue<bool>("Flee", "Q") && Q.CastOnBestTarget(0, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
        }

        private void AutoQ()
        {
            if (Player.ManaPercentage() < GetValue<Slider>("Harass", "AutoQMpA").Value || Q.CastOnBestTarget(0, PacketCast) == Spell.CastStates.SuccessfullyCasted) return;
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
            if (GetValue<bool>("KillSteal", "W") && W.IsReady())
            {
                var Target = W.GetTarget();
                if (Target != null && CanKill(Target, W) && W.CastOnUnit(Target, PacketCast)) return;
            }
        }

        private void AutoWUnderTower()
        {
            if (!W.IsReady()) return;
            var Target = ObjectManager.Get<Obj_AI_Hero>().FindAll(i => i.IsValidTarget(W.Range)).MinOrDefault(i => i.Distance(Player, true));
            var Tower = ObjectManager.Get<Obj_AI_Turret>().Find(i => i.IsAlly && !i.IsDead && i.Distance(Player, true) <= Math.Pow(950, 2));
            if (Target != null && Tower != null && Target.Distance(Tower, true) <= Math.Pow(950, 2) && W.CastOnUnit(Target, PacketCast)) return;
        }

        private double GetRDmg(Obj_AI_Base Target)
        {
            return Player.CalcDamage(Target, Damage.DamageType.Magical, new double[] { 100, 150, 200 }[R.Level - 1] + 0.5 * Player.FlatMagicDamageMod + R.Instance.Ammo);
        }
    }
}
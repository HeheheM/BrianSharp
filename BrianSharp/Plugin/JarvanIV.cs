using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    class JarvanIV : Common.Helper
    {
        private bool RCasted = false;

        public JarvanIV()
        {
            Q = new Spell(SpellSlot.Q, 880);
            W = new Spell(SpellSlot.W, 525);
            E = new Spell(SpellSlot.E, 860);
            R = new Spell(SpellSlot.R, 650);
            Q.SetSkillshot(0.25f, 70, 1450, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.5f, 175, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R.SetTargetted(0.5f, float.MaxValue);

            var ChampMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(ComboMenu, "Q", "Use Q");
                    AddItem(ComboMenu, "W", "Use W");
                    AddItem(ComboMenu, "WHpU", "-> If Player Hp Under", 40);
                    AddItem(ComboMenu, "WCountA", "-> If Enemy Above", 2, 1, 5);
                    AddItem(ComboMenu, "E", "Use E");
                    AddItem(ComboMenu, "R", "Use R");
                    AddItem(ComboMenu, "RHpU", "-> If Enemy Hp Under", 40);
                    AddItem(ComboMenu, "RCountA", "-> If Enemy Above", 2, 1, 5);
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(HarassMenu, "AutoQ", "Use Q", "H", KeyBindType.Toggle);
                    AddItem(HarassMenu, "AutoQMpA", "-> If Mp Above", 50);
                    AddItem(HarassMenu, "Q", "Use Q");
                    AddItem(HarassMenu, "QHpA", "-> To Flag If Hp Above", 20);
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
                    AddItem(ClearMenu, "WHpU", "-> If Hp Under", 40);
                    AddItem(ClearMenu, "E", "Use E");
                    AddItem(ClearMenu, "Item", "Use Tiamat/Hydra Item");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var LastHitMenu = new Menu("Last Hit", "LastHit");
                {
                    AddItem(LastHitMenu, "Q", "Use Q");
                    ChampMenu.AddSubMenu(LastHitMenu);
                }
                var FleeMenu = new Menu("Flee", "Flee");
                {
                    AddItem(FleeMenu, "EQ", "Use EQ");
                    AddItem(FleeMenu, "W", "Use W To Slow Enemy");
                    ChampMenu.AddSubMenu(FleeMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    var KillStealMenu = new Menu("Kill Steal", "KillSteal");
                    {
                        AddItem(KillStealMenu, "Q", "Use Q");
                        AddItem(KillStealMenu, "R", "Use R");
                        AddItem(KillStealMenu, "Ignite", "Use Ignite");
                        MiscMenu.AddSubMenu(KillStealMenu);
                    }
                    var InterruptMenu = new Menu("Interrupt", "Interrupt");
                    {
                        AddItem(InterruptMenu, "EQ", "Use EQ");
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
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Hero.OnProcessSpellCast += OnProcessSpellCast;
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit)
            {
                LastHit();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee) Flee();
            KillSteal();
            if (GetValue<KeyBind>("Harass", "AutoQ").Active) AutoQ();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (GetValue<bool>("Draw", "Q") && Q.Level > 0) Render.Circle.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "W") && W.Level > 0) Render.Circle.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "E") && E.Level > 0) Render.Circle.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "R") && R.Level > 0) Render.Circle.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnPossibleToInterrupt(Obj_AI_Hero unit, InterruptableSpell spell)
        {
            if (Player.IsDead || !GetValue<bool>("Interrupt", "EQ") || !GetValue<bool>("Interrupt", unit.ChampionName + "_" + spell.Slot.ToString()) || !Q.IsReady()) return;
            if (E.CanCast(unit) && Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost && E.Cast(unit.ServerPosition.Extend(Player.ServerPosition, -E.Width / 2), PacketCast) && Q.Cast(unit.ServerPosition, PacketCast)) return;
            if (Flag != null && (unit.Distance(Flag, true) <= Math.Pow(60, 2) || (Player.Distance(unit, true) >= Math.Pow(50, 2) && Q.WillHit(unit, Flag.ServerPosition, 110))) && Q.Cast(Flag.ServerPosition, PacketCast)) return;
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.Name == "JarvanIVCataclysm")
            {
                RCasted = true;
                Utility.DelayAction.Add(3500, () => RCasted = false);
            }
        }

        private void NormalCombo(string Mode)
        {
            if (GetValue<bool>(Mode, "E") && E.IsReady())
            {
                var Target = E.GetTarget();
                if (Target != null && E.Cast(Target.ServerPosition.Extend(Player.ServerPosition, -E.Width / 2), PacketCast))
                {
                    if (GetValue<bool>(Mode, "Q") && Q.IsReady() && (Mode == "Combo" || (Mode == "Harass" && Player.HealthPercentage() >= GetValue<Slider>(Mode, "QHpA").Value)) && Q.Cast(Target.ServerPosition, PacketCast)) return;
                    return;
                }
            }
            if ((!GetValue<bool>(Mode, "E") || (GetValue<bool>(Mode, "E") && !E.IsReady())) && GetValue<bool>(Mode, "Q") && Q.IsReady() && (Mode == "Combo" || (Mode == "Harass" && (Flag == null || (Flag != null && Player.HealthPercentage() >= GetValue<Slider>(Mode, "QHpA").Value)))))
            {
                var Target = Q.GetTarget();
                if (GetValue<bool>(Mode, "E") && Flag != null && (Target.Distance(Flag, true) <= Math.Pow(60, 2) || (Player.Distance(Target, true) >= Math.Pow(50, 2) && Q.WillHit(Target, Flag.ServerPosition, 110))) && Q.Cast(Flag.ServerPosition, PacketCast))
                {
                    return;
                }
                else if (Q.CastIfHitchanceEquals(Target, HitChance.High, PacketCast)) return;
            }
            if (Mode == "Combo")
            {
                if (GetValue<bool>(Mode, "R") && R.IsReady() && !RCasted)
                {
                    var Target = ObjectManager.Get<Obj_AI_Hero>().Find(i => i.IsValidTarget(R.Range) && ((i.CountEnemiesInRange(325) > 1 && CanKill(i, R)) || i.CountEnemiesInRange(325) >= GetValue<Slider>(Mode, "RCountA").Value || (i.CountEnemiesInRange(325) > 1 && i.GetEnemiesInRange(325).Count(a => a.HealthPercentage() < GetValue<Slider>(Mode, "RHpU").Value) > 0)));
                    if (Target != null && R.CastOnUnit(Target, PacketCast)) return;
                }
                if (GetValue<bool>(Mode, "W") && W.IsReady())
                {
                    var WCount = GetValue<Slider>(Mode, "WCountA").Value;
                    if (((WCount > 1 && (Player.CountEnemiesInRange(W.Range) >= WCount || W.GetTarget() != null)) || (WCount == 1 && W.GetTarget() != null)) && Player.HealthPercentage() < GetValue<Slider>(Mode, "WHpU").Value && W.Cast(PacketCast)) return;
                }
            }
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            if (minionObj.Count == 0) return;
            if (GetValue<bool>("Clear", "E") && E.IsReady() && (minionObj.Count > 1 || minionObj.Any(i => i.MaxHealth >= 1200)))
            {
                var Pos = E.GetCircularFarmLocation(minionObj.FindAll(i => E.IsInRange(i)));
                if (Pos.MinionsHit > 0 && Pos.Position.IsValid() && E.Cast(Pos.Position, PacketCast))
                {
                    if (GetValue<bool>("Clear", "Q") && Q.IsReady() && Q.Cast(Pos.Position, PacketCast)) return;
                    return;
                }
            }
            if ((!GetValue<bool>("Clear", "E") || (GetValue<bool>("Clear", "E") && !E.IsReady())) && GetValue<bool>("Clear", "Q") && Q.IsReady())
            {
                if (GetValue<bool>("Clear", "E") && Flag != null && (minionObj.Count(i => i.Distance(Flag, true) <= Math.Pow(60, 2)) > 1 || minionObj.Count(i => Q.WillHit(i, Flag.ServerPosition, 110)) > 1) && Q.Cast(Flag.ServerPosition, PacketCast))
                {
                    return;
                }
                else
                {
                    var Pos = Q.GetLineFarmLocation(minionObj);
                    if (Pos.MinionsHit > 0 && Pos.Position.IsValid() && Q.Cast(Pos.Position, PacketCast)) return;
                }
            }
            if (GetValue<bool>("Clear", "W") && W.IsReady() && Player.HealthPercentage() < GetValue<Slider>("Clear", "WHpU").Value && minionObj.Count(i => W.IsInRange(i)) > 0 && W.Cast(PacketCast)) return;
            if (GetValue<bool>("Clear", "Item"))
            {
                var Item = Hydra.IsReady() ? Hydra : Tiamat;
                if (Item.IsReady() && (minionObj.Count(i => Item.IsInRange(i)) > 2 || minionObj.Any(i => i.MaxHealth >= 1200 && i.Distance(Player, true) <= Math.Pow(Item.Range - 80, 2))) && Item.Cast()) return;
            }
            if (GetValue<bool>("SmiteMob", "Smite") && Smite.IsReady())
            {
                var Obj = minionObj.Find(i => i.Team == GameObjectTeam.Neutral && CanSmiteMob(i.Name));
                if (Obj != null && CastSmite(Obj)) return;
            }
        }

        private void LastHit()
        {
            if (!GetValue<bool>("LastHit", "Q") || !Q.IsReady()) return;
            var minionObj = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth).FindAll(i => CanKill(i, Q));
            if (minionObj.Count == 0 || Q.CastIfHitchanceEquals(minionObj.First(), HitChance.High, PacketCast)) return;
        }

        private void Flee()
        {
            if (GetValue<bool>("Flee", "EQ") && Q.IsReady() && E.IsReady() && Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost && E.Cast(Game.CursorPos, PacketCast) && Q.Cast(Game.CursorPos, PacketCast)) return;
            if (GetValue<bool>("Flee", "W") && W.IsReady() && !Q.IsReady() && W.GetTarget() != null && W.Cast(PacketCast)) return;
        }

        private void AutoQ()
        {
            if (Player.ManaPercentage() < GetValue<Slider>("Harass", "AutoQMpA").Value || Q.CastOnBestTarget(0, PacketCast).IsCasted()) return;
        }

        private void KillSteal()
        {
            if (R.IsReady() && RCasted && Player.CountEnemiesInRange(325) == 0) R.Cast(PacketCast);
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
            if (GetValue<bool>("KillSteal", "R") && R.IsReady())
            {
                var Target = R.GetTarget();
                if (Target != null && CanKill(Target, R) && R.CastOnUnit(Target, PacketCast)) return;
            }
        }

        private Obj_AI_Minion Flag
        {
            get { return ObjectManager.Get<Obj_AI_Minion>().Find(i => i.IsAlly && Q.IsInRange(i) && i.Name == "Beacon"); }
        }
    }
}
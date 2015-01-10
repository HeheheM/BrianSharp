using System;
using System.Linq;
using System.Collections.Generic;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    public class Lucian : Common.Helper
    {
        private bool QCasted = false, WCasted = false, ECasted = false, WillInAA = false;
        private Obj_AI_Hero RTarget = null;
        private Vector3 REndPos = new Vector3();
        private bool RKillable = false;

        public Lucian()
        {
            Q = new Spell(SpellSlot.Q, 630);
            Q2 = new Spell(SpellSlot.Q, 1130);
            W = new Spell(SpellSlot.W, 1080);
            E = new Spell(SpellSlot.E, 445);
            R = new Spell(SpellSlot.R, 1460);
            Q.SetTargetted(0.35f, float.MaxValue);
            Q2.SetSkillshot(0.35f, 65, float.MaxValue, false, SkillshotType.SkillshotLine);
            W.SetSkillshot(0.33f, 80, 1470, true, SkillshotType.SkillshotLine);
            R.SetSkillshot(0.2f, 60, 2900, true, SkillshotType.SkillshotLine);

            var ChampMenu = new Menu("Plugin", PlayerName + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Passive", "Use Passive");
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "ExtendQ", "-> Extend Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "PredW", "-> W Prediction");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "GapE", "-> Gap Closer");
                    ItemSlider(ComboMenu, "EDelay", "-> Stop Q/W If E Will Ready In (ms)", 2000, 0, 4000);
                    ItemBool(ComboMenu, "R", "Use R If Killable");
                    ItemBool(ComboMenu, "YoumuuR", "-> Use Youmuu For Max Damage");
                    ItemBool(ComboMenu, "CancelR", "-> Stop R For Kill Steal");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Passive", "Use Passive");
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemBool(HarassMenu, "E", "Use E");
                    ItemSlider(HarassMenu, "EAbove", "-> If Hp Above", 20);
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "W", "Use W");
                    ItemBool(ClearMenu, "E", "Use E");
                    ItemSlider(ClearMenu, "EDelay", "-> Stop Q/W If E Will Ready In (ms)", 2000, 0, 4000);
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QKillSteal", "Use Q To Kill Steal");
                    ItemBool(MiscMenu, "WKillSteal", "Use W To Kill Steal");
                    ItemBool(MiscMenu, "IgniteKillSteal", "Use Ignite To Kill Steal");
                    ItemBool(MiscMenu, "LockR", "Lock R On Target");
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
                    ItemBool(DrawMenu, "W", "W Range", false);
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ItemBool(DrawMenu, "R", "R Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                MainMenu.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Obj_AI_Hero.OnProcessSpellCast += OnProcessSpellCast;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling()) return;
            KillSteal();
            if (Player.IsChannelingImportantSpell())
            {
                LockROnTarget();
                return;
            }
            else
            {
                RTarget = null;
                REndPos = new Vector3();
                RKillable = false;
            }
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear) LaneJungClear();
            if (Orbwalk.CurrentMode != Orbwalk.Mode.Combo && Orbwalk.CurrentMode != Orbwalk.Mode.LaneClear) WillInAA = false;
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Render.Circle.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red, 7);
            if (ItemBool("Draw", "W") && W.Level > 0) Render.Circle.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red, 7);
            if (ItemBool("Draw", "E") && E.Level > 0) Render.Circle.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red, 7);
            if (ItemBool("Draw", "R") && R.Level > 0) Render.Circle.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red, 7);
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.Name == "LucianQ")
            {
                QCasted = true;
                Utility.DelayAction.Add(350, () => QCasted = false);
            }
            if (args.SData.Name == "LucianW")
            {
                WCasted = true;
                Utility.DelayAction.Add(330, () => WCasted = false);
            }
            if (args.SData.Name == "LucianE")
            {
                ECasted = true;
                Utility.DelayAction.Add(280, () => ECasted = false);
            }
            if (args.SData.Name == "LucianR" && !RKillable) REndPos = (Player.ServerPosition - (Player.ServerPosition.To2D() + R.Range * Player.Direction.To2D().Perpendicular()).To3D()).Normalized();
        }

        private void AfterAttack(AttackableUnit Target)
        {
            if (!E.IsReady()) return;
            if ((Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear && ItemBool("Clear", "E") && !HavePassive() && Target is Obj_AI_Minion) || ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || (Orbwalk.CurrentMode == Orbwalk.Mode.Harass && Player.HealthPercentage() >= ItemSlider("Harass", "EAbove"))) && ItemBool(Orbwalk.CurrentMode.ToString(), "E") && !HavePassive(Orbwalk.CurrentMode.ToString()) && Target is Obj_AI_Hero))
            {
                var Pos = (Player.Distance(Game.CursorPos) <= E.Range && Player.Distance(Game.CursorPos) > 100) ? Game.CursorPos : Player.ServerPosition.Extend(Game.CursorPos, E.Range);
                if (((Obj_AI_Base)Target).Distance(Pos) <= Orbwalk.GetAutoAttackRange(Player, Target))
                {
                    E.Cast(Pos, PacketCast);
                    if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear) WillInAA = true;
                }
                WillInAA = false;
            }
        }

        private void NormalCombo(string Mode)
        {
            if (TS.Target == null || Player.IsDashing()) return;
            if (ItemBool(Mode, "Q") && Q.IsReady() && CanKill(TS.Target, Q))
            {
                if (Q.IsInRange(TS.Target))
                {
                    Q.CastOnUnit(TS.Target, PacketCast);
                }
                else if (Q2.IsInRange(TS.Target) && GetQ2Collision(TS.Target) != null) Q.CastOnUnit(GetQ2Collision(TS.Target), PacketCast);
            }
            if (ItemBool(Mode, "W") && W.CanCast(TS.Target) && CanKill(TS.Target, W))
            {
                if (W.GetPrediction(TS.Target).Hitchance >= HitChance.Medium)
                {
                    W.Cast(W.GetPrediction(TS.Target).CastPosition, PacketCast);
                }
                else
                {
                    foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget() && !(i is Obj_AI_Turret) && i.Distance(TS.Target, true) <= W.WidthSqr && W.GetPrediction(i).Hitchance >= HitChance.Medium)) W.Cast(W.GetPrediction(Obj).CastPosition, PacketCast);
                }
            }
            if (Mode == "Combo" && ItemBool(Mode, "R") && R.CanCast(TS.Target) && CanKill(TS.Target, R, GetRDmg(TS.Target)))
            {
                if (Player.Distance(TS.Target, true) > Math.Pow(500, 2) && Player.Distance(TS.Target, true) <= Math.Pow(800, 2) && (!ItemBool(Mode, "Q") || (ItemBool(Mode, "Q") && !Q.IsReady())) && (!ItemBool(Mode, "W") || (ItemBool(Mode, "W") && !W.IsReady())) && (!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && !E.IsReady())))
                {
                    R.Cast(TS.Target.ServerPosition, PacketCast);
                    RTarget = TS.Target;
                    RKillable = true;
                    REndPos = (Player.ServerPosition - TS.Target.ServerPosition).Normalized();
                    if (ItemBool(Mode, "YoumuuR") && Youmuu.IsReady()) Utility.DelayAction.Add(10, () => Youmuu.Cast());
                }
                else if (Player.Distance(TS.Target, true) > Math.Pow(800, 2) && Player.Distance(TS.Target, true) <= Math.Pow(1075, 2))
                {
                    R.Cast(TS.Target.ServerPosition, PacketCast);
                    RTarget = TS.Target;
                    RKillable = true;
                    REndPos = (Player.ServerPosition - TS.Target.ServerPosition).Normalized();
                    if (ItemBool(Mode, "YoumuuR") && Youmuu.IsReady()) Utility.DelayAction.Add(10, () => Youmuu.Cast());
                }
            }
            if (Mode == "Combo" && ItemBool(Mode, "E") && ItemBool(Mode, "GapE") && E.IsReady() && !Orbwalk.InAutoAttackRange(TS.Target) && TS.Target.Distance(Player.ServerPosition.Extend(Game.CursorPos, E.Range)) + 20 <= Orbwalk.GetAutoAttackRange(Player, TS.Target)) E.Cast(Game.CursorPos, PacketCast);
            if (!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && (!E.IsReady() || (Mode == "Combo" && E.IsReady() && !WillInAA && Orbwalk.InAutoAttackRange(TS.Target)))))
            {
                if (Mode == "Combo" && ItemBool(Mode, "E") && E.IsReady(ItemSlider(Mode, "EDelay"))) return;
                if (ItemBool(Mode, "Q") && Q.IsReady())
                {
                    if ((Orbwalk.InAutoAttackRange(TS.Target) && !HavePassive(Mode)) || (Player.Distance(TS.Target, true) > Math.Pow(Orbwalk.GetAutoAttackRange(Player, TS.Target) + 40, 2) && Q.IsInRange(TS.Target)))
                    {
                        Q.CastOnUnit(TS.Target, PacketCast);
                    }
                    else if ((Mode == "Harass" || (Mode == "Combo" && ItemBool(Mode, "ExtendQ"))) && !Q.IsInRange(TS.Target) && Q2.IsInRange(TS.Target) && GetQ2Collision(TS.Target) != null) Q.CastOnUnit(GetQ2Collision(TS.Target), PacketCast);
                }
                if ((!ItemBool(Mode, "Q") || (ItemBool(Mode, "Q") && !Q.IsReady())) && ItemBool(Mode, "W") && W.IsReady() && ((Orbwalk.InAutoAttackRange(TS.Target) && !HavePassive(Mode)) || (Player.Distance(TS.Target, true) > Math.Pow(Orbwalk.GetAutoAttackRange(Player, TS.Target) + 40, 2) && W.IsInRange(TS.Target))))
                {
                    if (Mode == "Harass" || (Mode == "Combo" && ItemBool(Mode, "PredW")))
                    {
                        if (W.GetPrediction(TS.Target).Hitchance >= HitChance.Medium)
                        {
                            W.Cast(W.GetPrediction(TS.Target).CastPosition, PacketCast);
                        }
                        else
                        {
                            foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget() && !(i is Obj_AI_Turret) && i.Distance(TS.Target, true) <= W.WidthSqr && W.GetPrediction(i).Hitchance >= HitChance.Medium)) W.Cast(W.GetPrediction(Obj).CastPosition, PacketCast);
                        }
                    }
                    else if (Mode == "Combo" && !ItemBool(Mode, "PredW")) W.Cast(W.GetPrediction(TS.Target).CastPosition, PacketCast);
                }
            }
        }

        private void LaneJungClear()
        {
            if (Player.IsDashing()) return;
            var minionObj = MinionManager.GetMinions(Q2.Range, MinionTypes.All, MinionTeam.NotAlly);
            if (!ItemBool("Clear", "E") || (ItemBool("Clear", "E") && (!E.IsReady() || (E.IsReady() && !WillInAA && Orbwalk.InAutoAttackRange(Orbwalk.GetPossibleTarget())))))
            {
                if (ItemBool("Clear", "E") && E.IsReady(ItemSlider("Clear", "EDelay"))) return;
                if (ItemBool("Clear", "W") && W.IsReady() && !HavePassive())
                {
                    var ClearWPos = W.GetCircularFarmLocation(minionObj.Where(i => W.IsInRange(i)).ToList());
                    if (ClearWPos.MinionsHit > 0) W.Cast(ClearWPos.Position, PacketCast);
                }
                if ((!ItemBool("Clear", "W") || (ItemBool("Clear", "W") && !W.IsReady())) && ItemBool("Clear", "Q") && Q.IsReady() && !HavePassive())
                {
                    var ClearQPos = Q2.GetLineFarmLocation(minionObj);
                    if (ClearQPos.MinionsHit > 0)
                    {
                        var Obj = minionObj.FirstOrDefault(i => Q.IsInRange(i) && Q2.WillHit(i, ClearQPos.Position.To3D(), 0, HitChance.VeryHigh));
                        if (Obj != null) Q.CastOnUnit(Obj, PacketCast);
                    }
                }
            }
        }

        private void KillSteal()
        {
            if (Player.IsDashing()) return;
            if (ItemBool("Misc", "QKillSteal") || ItemBool("Misc", "WKillSteal") || ItemBool("Misc", "IgniteKillSteal"))
            {
                var CancelR = ItemBool("Combo", "R") && ItemBool("Combo", "CancelR") && Player.IsChannelingImportantSpell() && R.IsReady();
                foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(Q2.Range)).OrderBy(i => i.Health).OrderBy(i => i.Distance(Player, true)))
                {
                    if (Obj != TS.Target)
                    {
                        if (ItemBool("Misc", "IgniteKillSteal")) CastIgnite(Obj);
                        if ((!ItemBool("Combo", "R") || (ItemBool("Combo", "R") && !ItemBool("Combo", "CancelR"))) && Player.IsChannelingImportantSpell()) break;
                        if (ItemBool("Misc", "QKillSteal") && Q.IsReady() && CanKill(Obj, Q))
                        {
                            if (Q.IsInRange(Obj))
                            {
                                if (CancelR) R.Cast(PacketCast);
                                Q.CastOnUnit(Obj, PacketCast);
                            }
                            else if (GetQ2Collision(Obj) != null)
                            {
                                if (CancelR) R.Cast(PacketCast);
                                Q.CastOnUnit(GetQ2Collision(Obj), PacketCast);
                            }
                        }
                        if (ItemBool("Misc", "WKillSteal") && W.CanCast(Obj) && CanKill(Obj, W))
                        {
                            if (W.GetPrediction(Obj).Hitchance >= HitChance.Medium)
                            {
                                if (CancelR) R.Cast(PacketCast);
                                W.Cast(W.GetPrediction(Obj).CastPosition, PacketCast);
                            }
                            else
                            {
                                foreach (var Col in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget() && !(i is Obj_AI_Turret) && i.Distance(Obj, true) <= W.WidthSqr && W.GetPrediction(i).Hitchance >= HitChance.Medium))
                                {
                                    if (CancelR) R.Cast(PacketCast);
                                    W.Cast(W.GetPrediction(Col).CastPosition, PacketCast);
                                }
                            }
                        }
                    }
                    else if (ItemBool("Misc", "IgniteKillSteal") && (!ItemBool("Combo", "Q") || (ItemBool("Combo", "Q") && !Q.IsReady())) && (!ItemBool("Combo", "W") || (ItemBool("Combo", "W") && !W.IsReady())) && (!ItemBool("Combo", "R") || (ItemBool("Combo", "R") && !R.IsReady()))) Utility.DelayAction.Add(1000, () => CastIgnite(Obj));
                }
            }
        }

        private bool HavePassive(string Mode = "Clear")
        {
            if (Mode != "Clear" && !ItemBool(Mode, "Passive")) return false;
            if (QCasted || WCasted || ECasted || Player.HasBuff("LucianPassiveBuff")) return true;
            return false;
        }

        private double GetRDmg(Obj_AI_Hero Target)
        {
            var Shot = (int)(7.5 + new double[] { 7.5, 9, 10.5 }[R.Level - 1] * 1 / Player.AttackDelay);
            var MaxShot = new int[] { 26, 30, 33 }[R.Level - 1];
            return Player.CalcDamage(Target, Damage.DamageType.Physical, (new double[] { 40, 50, 60 }[R.Level - 1] + 0.25 * Player.FlatPhysicalDamageMod + 0.1 * Player.FlatMagicDamageMod) * (Shot > MaxShot ? MaxShot : Shot));
        }

        private void LockROnTarget()
        {
            if (ItemBool("Misc", "LockR") && R.IsReady())
            {
                var Target = RTarget.IsValidTarget() ? RTarget : TS.Target;
                if (!Target.IsValidTarget() || !REndPos.IsValid())
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
                    return;
                }
                var Pos = R.GetPrediction(Target).CastPosition;
                var FullPoint = new Vector2(Pos.X + REndPos.X * R.Range, Pos.Y + REndPos.Y * R.Range).To3D();
                var MidPoint = new Vector2((FullPoint.X * 2 - Pos.X) / Pos.Distance(FullPoint) * R.Range, (FullPoint.Y * 2 - Pos.Y) / Pos.Distance(FullPoint) * R.Range).To3D();
                var ClosestPoint = Player.ServerPosition.To2D().Closest(new List<Vector3> { Pos, FullPoint }.To2D()).To3D();
                if (ClosestPoint.IsValid() && !ClosestPoint.IsWall() && Pos.Distance(ClosestPoint) > E.Range)
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, ClosestPoint);
                }
                else if (FullPoint.IsValid() && !FullPoint.IsWall() && Pos.Distance(FullPoint) < R.Range && Pos.Distance(FullPoint) > 100)
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, FullPoint);
                }
                else if (MidPoint.IsValid() && !MidPoint.IsWall())
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, MidPoint);
                }
                else Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
            }
            else Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
        }

        private Obj_AI_Base GetQ2Collision(Obj_AI_Hero Target)
        {
            var Pred = new PredictionInput
            {
                Range = Q.Range,
                Delay = Q2.Delay,
                Radius = Q2.Width,
                Speed = Q2.Speed,
                CollisionObjects = new CollisionableObjects[] { CollisionableObjects.Heroes, CollisionableObjects.Minions }
            };
            return LeagueSharp.Common.Collision.GetCollision(new List<Vector3> { Target.ServerPosition }, Pred).FirstOrDefault(i => Q2.WillHit(i, Target.ServerPosition, 0, HitChance.VeryHigh));
        }
    }
}
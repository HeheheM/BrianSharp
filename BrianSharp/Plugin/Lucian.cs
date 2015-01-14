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
        private bool QCasted = false, WCasted = false, ECasted = false;
        private Obj_AI_Hero RTarget = null;
        private Vector3 REndPos = default(Vector3);
        private bool RKillable = false;

        public Lucian()
        {
            Q = new Spell(SpellSlot.Q, 630);
            Q2 = new Spell(SpellSlot.Q, 1130);
            W = new Spell(SpellSlot.W, 1080, TargetSelector.DamageType.Magical);
            E = new Spell(SpellSlot.E, 445);
            R = new Spell(SpellSlot.R, 1460);
            Q.SetTargetted(0.5f, 500);
            Q2.SetSkillshot(0.5f, 65, 500, false, SkillshotType.SkillshotLine);
            W.SetSkillshot(0.5f, 80, 500, true, SkillshotType.SkillshotLine);
            R.SetSkillshot(0.5f, 60, float.MaxValue, true, SkillshotType.SkillshotLine);

            var ChampMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(ComboMenu, "Passive", "Use Passive");
                    AddItem(ComboMenu, "HoldPassive", "-> Always Hold", false);
                    AddItem(ComboMenu, "Q", "Use Q");
                    AddItem(ComboMenu, "ExtendQ", "-> Extend Q");
                    AddItem(ComboMenu, "W", "Use W");
                    AddItem(ComboMenu, "PredW", "-> W Prediction");
                    AddItem(ComboMenu, "E", "Use E");
                    AddItem(ComboMenu, "GapE", "-> Gap Closer");
                    AddItem(ComboMenu, "EDelay", "-> Stop Q/W If E Will Ready In (ms)", 500, 0, 1000);
                    AddItem(ComboMenu, "EMode", "-> Mode", new[] { "Safe", "Mouse", "Chase" });
                    AddItem(ComboMenu, "EModeKey", "--> Key Switch", "Z", KeyBindType.Toggle).ValueChanged += ComboEModeChanged;
                    AddItem(ComboMenu, "EModeDraw", "--> Draw Text", false);
                    AddItem(ComboMenu, "R", "Use R If Killable");
                    AddItem(ComboMenu, "YoumuuR", "-> Use Youmuu For More Damage");
                    AddItem(ComboMenu, "CancelR", "-> Stop R For Kill Steal");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(HarassMenu, "AutoQ", "Use Extend Q", "H", KeyBindType.Toggle);
                    AddItem(HarassMenu, "AutoQMana", "-> If Mp Above", 50);
                    AddItem(HarassMenu, "Passive", "Use Passive");
                    AddItem(HarassMenu, "HoldPassive", "-> Always Hold", false);
                    AddItem(HarassMenu, "Q", "Use Q");
                    AddItem(HarassMenu, "W", "Use W");
                    AddItem(HarassMenu, "E", "Use E");
                    AddItem(HarassMenu, "EAbove", "-> If Hp Above", 20);
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    AddItem(ClearMenu, "Q", "Use Q");
                    AddItem(ClearMenu, "W", "Use W");
                    AddItem(ClearMenu, "E", "Use E");
                    AddItem(ClearMenu, "EDelay", "-> Stop Q/W If E Will Ready In (ms)", 500, 0, 1000);
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    AddItem(MiscMenu, "QKillSteal", "Use Q To Kill Steal");
                    AddItem(MiscMenu, "WKillSteal", "Use W To Kill Steal");
                    AddItem(MiscMenu, "IgniteKillSteal", "Use Ignite To Kill Steal");
                    AddItem(MiscMenu, "LockR", "Lock R On Target");
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
            Obj_AI_Hero.OnProcessSpellCast += OnProcessSpellCast;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void ComboEModeChanged(object sender, OnValueChangeEventArgs e)
        {
            switch (GetValue<StringList>("Combo", "EMode").SelectedIndex)
            {
                case 0:
                    GetItem("Combo", "EMode").SetValue(new StringList(GetValue<StringList>("Combo", "EMode").SList, 1));
                    break;
                case 1:
                    GetItem("Combo", "EMode").SetValue(new StringList(GetValue<StringList>("Combo", "EMode").SList, 2));
                    break;
                case 2:
                    GetItem("Combo", "EMode").SetValue(new StringList(GetValue<StringList>("Combo", "EMode").SList, 0));
                    break;
            }
            e.Process = false;
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
                REndPos = default(Vector3);
                RKillable = false;
            }
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear) LaneJungClear();
            if (GetValue<KeyBind>("Harass", "AutoQ").Active) AutoQ();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (GetValue<bool>("Combo", "E") && GetValue<bool>("Combo", "EModeDraw"))
            {
                var Pos = Drawing.WorldToScreen(Player.Position);
                Drawing.DrawText(Pos.X, Pos.Y, Color.Orange, GetValue<StringList>("Combo", "EMode").SelectedValue);
            }
            if (GetValue<bool>("Draw", "Q") && Q.Level > 0) Render.Circle.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "W") && W.Level > 0) Render.Circle.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "E") && E.Level > 0) Render.Circle.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            if (GetValue<bool>("Draw", "R") && R.Level > 0) Render.Circle.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.Name == "LucianQ")
            {
                QCasted = true;
                Utility.DelayAction.Add(350, () => QCasted = false);
                if (((Obj_AI_Base)args.Target).IsValidTarget() && Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear ? args.Target is Obj_AI_Minion : args.Target is Obj_AI_Hero) Utility.DelayAction.Add(300, () => Player.IssueOrder(GameObjectOrder.AttackUnit, args.Target));
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
            if (!E.IsReady() || !Target.IsValidTarget()) return;
            if ((Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear && GetValue<bool>("Clear", "E") && !HavePassive() && Target is Obj_AI_Minion) || ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || (Orbwalk.CurrentMode == Orbwalk.Mode.Harass && Player.HealthPercentage() >= GetValue<Slider>("Harass", "EAbove").Value)) && GetValue<bool>(Orbwalk.CurrentMode.ToString(), "E") && !HavePassive(Orbwalk.CurrentMode.ToString()) && Target is Obj_AI_Hero))
            {
                if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.Harass || (Orbwalk.CurrentMode == Orbwalk.Mode.Combo && GetValue<StringList>("Combo", "EMode").SelectedIndex == 0))
                {
                    var Pos = Geometry.CircleCircleIntersection(Player.ServerPosition.To2D(), ((Obj_AI_Base)Target).ServerPosition.To2D(), E.Range, Orbwalk.GetAutoAttackRange(Player, Target) - 30);
                    if (Pos.Count() > 0)
                    {
                        E.Cast(Pos.MinOrDefault(i => i.Distance(Game.CursorPos)), PacketCast);
                    }
                    else E.Cast(Player.ServerPosition.Extend(((Obj_AI_Base)Target).ServerPosition, -E.Range), PacketCast);
                }
                else if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo)
                {
                    switch (GetValue<StringList>("Combo", "EMode").SelectedIndex)
                    {
                        case 1:
                            E.Cast(Player.ServerPosition.Extend(Game.CursorPos, E.Range), PacketCast);
                            break;
                        case 2:
                            E.Cast(((Obj_AI_Base)Target).ServerPosition, PacketCast);
                            break;
                    }
                }
            }
        }

        private void NormalCombo(string Mode)
        {
            if (Player.IsDashing()) return;
            if (Mode == "Combo" && GetValue<bool>(Mode, "R") && R.IsReady())
            {
                var Target = R.GetTarget();
                if (Target != null && CanKill(Target, R, GetRDmg(Target)))
                {
                    if ((Player.Distance(Target, true) > Math.Pow(800, 2) && Player.Distance(Target, true) <= Math.Pow(1075, 2)) || (!Orbwalk.InAutoAttackRange(Target) && Player.Distance(Target, true) <= Math.Pow(800, 2) && (!GetValue<bool>(Mode, "Q") || (GetValue<bool>(Mode, "Q") && !Q.IsReady())) && (!GetValue<bool>(Mode, "W") || (GetValue<bool>(Mode, "W") && !W.IsReady())) && (!GetValue<bool>(Mode, "E") || (GetValue<bool>(Mode, "E") && !E.IsReady()))))
                    {
                        R.Cast(R.GetPrediction(Target).CastPosition, PacketCast);
                        RTarget = Target;
                        REndPos = (Player.ServerPosition - Target.ServerPosition).Normalized();
                        RKillable = true;
                        if (GetValue<bool>(Mode, "YoumuuR") && Youmuu.IsReady()) Utility.DelayAction.Add(10, () => Youmuu.Cast());
                        return;
                    }
                }
            }
            if (Mode == "Combo" && GetValue<bool>(Mode, "E") && GetValue<bool>(Mode, "GapE") && E.IsReady())
            {
                var Target = E.GetTarget(Orbwalk.GetAutoAttackRange());
                if (Target != null && !Orbwalk.InAutoAttackRange(Target) && Target.Distance(Player.ServerPosition.Extend(Game.CursorPos, E.Range)) + 20 <= Orbwalk.GetAutoAttackRange(Player, Target))
                {
                    E.Cast(Player.ServerPosition.Extend(Game.CursorPos, E.Range), PacketCast);
                    return;
                }
            }
            if (HavePassive(Mode) && GetValue<bool>(Mode, "HoldPassive")) return;
            if (!GetValue<bool>(Mode, "E") || (GetValue<bool>(Mode, "E") && !E.IsReady()))
            {
                if (Mode == "Combo" && GetValue<bool>(Mode, "E") && E.IsReady(GetValue<Slider>(Mode, "EDelay").Value)) return;
                if (GetValue<bool>(Mode, "Q") && Q.IsReady())
                {
                    var Target = Q.GetTarget();
                    if (Target == null) Target = Q2.GetTarget();
                    if (Target != null)
                    {
                        if ((Orbwalk.InAutoAttackRange(Target) && !HavePassive(Mode)) || (Player.Distance(Target, true) > Math.Pow(Orbwalk.GetAutoAttackRange(Player, Target) + 40, 2) && Q.IsInRange(Target)))
                        {
                            Q.CastOnUnit(Target, PacketCast);
                            return;
                        }
                        else if ((Mode == "Harass" || (Mode == "Combo" && GetValue<bool>(Mode, "ExtendQ"))) && !Q.IsInRange(Target)) CastExtendQ(Target);
                    }
                }
                if ((!GetValue<bool>(Mode, "Q") || (GetValue<bool>(Mode, "Q") && !Q.IsReady())) && GetValue<bool>(Mode, "W") && W.IsReady())
                {
                    var Target = W.GetTarget();
                    if (Target != null && ((Orbwalk.InAutoAttackRange(Target) && !HavePassive(Mode)) || (Player.Distance(Target, true) > Math.Pow(Orbwalk.GetAutoAttackRange(Player, Target) + 40, 2))))
                    {
                        if (Mode == "Harass" || (Mode == "Combo" && GetValue<bool>(Mode, "PredW")))
                        {
                            if (W.GetPrediction(Target).Hitchance >= HitChance.Medium)
                            {
                                W.Cast(W.GetPrediction(Target).CastPosition, PacketCast);
                                return;
                            }
                            else
                            {
                                foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget() && !(i is Obj_AI_Turret) && i != Target && i.Distance(Target, true) <= W.WidthSqr && W.GetPrediction(i).Hitchance >= HitChance.Medium))
                                {
                                    W.Cast(W.GetPrediction(Obj).CastPosition, PacketCast);
                                    return;
                                }
                            }
                        }
                        else if (Mode == "Combo" && !GetValue<bool>(Mode, "PredW"))
                        {
                            W.Cast(W.GetPrediction(Target).CastPosition, PacketCast);
                            return;
                        }
                    }
                }
            }
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Q2.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            if (minionObj.Count == 0 || Player.IsDashing()) return;
            if (!GetValue<bool>("Clear", "E") || (GetValue<bool>("Clear", "E") && !E.IsReady()))
            {
                if (GetValue<bool>("Clear", "E") && E.IsReady(GetValue<Slider>("Clear", "EDelay").Value)) return;
                if (GetValue<bool>("Clear", "W") && W.IsReady() && !HavePassive())
                {
                    var Pos = W.GetLineFarmLocation(minionObj.Where(i => W.IsInRange(i)).ToList(), W.Width / 2);
                    if (Pos.MinionsHit > 0)
                    {
                        W.Cast(Pos.Position, PacketCast);
                        return;
                    }
                }
                if ((!GetValue<bool>("Clear", "W") || (GetValue<bool>("Clear", "W") && !W.IsReady())) && GetValue<bool>("Clear", "Q") && Q.IsReady() && !HavePassive())
                {
                    var Pos = Q2.GetLineFarmLocation(minionObj);
                    if (Pos.MinionsHit > 0)
                    {
                        var Obj = minionObj.FirstOrDefault(i => Q.IsInRange(i) && Q2.WillHit(i.ServerPosition, Pos.Position.To3D().Extend(Player.ServerPosition, -Q2.Range)));
                        if (Obj != null)
                        {
                            Q.CastOnUnit(Obj, PacketCast);
                            return;
                        }
                    }
                }
            }
        }

        private void KillSteal()
        {
            if (GetValue<bool>("Misc", "IgniteKillSteal") && Ignite.IsReady())
            {
                var Target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.True);
                if (Target != null) CastIgnite(Target);
            }
            if (Player.IsDashing() || ((!GetValue<bool>("Combo", "R") || (GetValue<bool>("Combo", "R") && !GetValue<bool>("Combo", "CancelR"))) && Player.IsChannelingImportantSpell())) return;
            var CancelR = GetValue<bool>("Combo", "R") && GetValue<bool>("Combo", "CancelR") && Player.IsChannelingImportantSpell() && R.IsReady();
            if (GetValue<bool>("Misc", "QKillSteal") && Q.IsReady())
            {
                var Target = Q.GetTarget();
                if (Target == null) Target = Q2.GetTarget();
                if (Target != null && CanKill(Target, Q))
                {
                    if (Q.IsInRange(Target))
                    {
                        if (CancelR) R.Cast(PacketCast);
                        Q.CastOnUnit(Target, PacketCast);
                        return;
                    }
                    else CastExtendQ(Target, CancelR);
                }
            }
            if (GetValue<bool>("Misc", "WKillSteal") && W.IsReady())
            {
                var Target = W.GetTarget();
                if (Target != null && CanKill(Target, W) && W.GetPrediction(Target).Hitchance >= HitChance.Medium)
                {
                    if (CancelR) R.Cast(PacketCast);
                    W.Cast(W.GetPrediction(Target).CastPosition, PacketCast);
                    return;
                }
            }
        }

        private void AutoQ()
        {
            if (Player.IsDashing() || !Q.IsReady() || Player.ManaPercentage() < GetValue<Slider>("Harass", "AutoQMana").Value) return;
            var Target = Q2.GetTarget();
            if (Target != null && !Q.IsInRange(Target)) CastExtendQ(Target);
        }

        private void LockROnTarget()
        {
            if (GetValue<bool>("Misc", "LockR") && R.IsReady())
            {
                var Target = RTarget.IsValidTarget() ? RTarget : R.GetTarget();
                if (Target == null || REndPos == default(Vector3))
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
                    return;
                }
                var Pos = R.GetPrediction(Target).CastPosition;
                var FullPoint = new Vector2(Pos.X + REndPos.X * R.Range * 0.98f, Pos.Y + REndPos.Y * R.Range * 0.98f).To3D();
                var MidPoint = new Vector2((FullPoint.X * 2 - Pos.X) / Pos.Distance(FullPoint) * R.Range * 0.98f, (FullPoint.Y * 2 - Pos.Y) / Pos.Distance(FullPoint) * R.Range * 0.98f).To3D();
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

        private void CastExtendQ(Obj_AI_Hero Target, bool CancelR = false)
        {
            foreach (var Obj in MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly))
            {
                if (Q2.WillHit(Target.ServerPosition, Obj.ServerPosition.Extend(Player.ServerPosition, -Q2.Range)))
                {
                    if (CancelR) R.Cast(PacketCast);
                    Q.CastOnUnit(Obj, PacketCast);
                }
            }
        }

        private bool HavePassive(string Mode = "Clear")
        {
            if (Mode != "Clear" && !GetValue<bool>(Mode, "Passive")) return false;
            if (QCasted || WCasted || ECasted || Player.HasBuff("LucianPassiveBuff")) return true;
            return false;
        }

        private double GetRDmg(Obj_AI_Hero Target)
        {
            var Shot = (int)(7.5 + new double[] { 7.5, 9, 10.5 }[R.Level - 1] * 1 / Player.AttackDelay);
            var MaxShot = new int[] { 26, 30, 33 }[R.Level - 1];
            return Player.CalcDamage(Target, Damage.DamageType.Physical, (new double[] { 40, 50, 60 }[R.Level - 1] + 0.25 * Player.FlatPhysicalDamageMod + 0.1 * Player.FlatMagicDamageMod) * (Shot > MaxShot ? MaxShot : Shot));
        }
    }
}
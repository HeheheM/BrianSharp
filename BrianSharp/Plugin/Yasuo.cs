using System;
using System.Collections.Generic;
using System.Linq;
using BrianSharp.Common;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;
using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    internal class Yasuo : Helper
    {
        private bool _isDashing;

        public Yasuo()
        {
            Q = new Spell(SpellSlot.Q, 482);
            Q2 = new Spell(SpellSlot.Q, 1000);
            W = new Spell(SpellSlot.W, 400);
            E = new Spell(SpellSlot.E, 484);
            R = new Spell(SpellSlot.R, 1200);
            Q.SetSkillshot(GetQDelay * 0.3f, 55, 1500, false, SkillshotType.SkillshotLine);
            Q2.SetSkillshot(GetQDelay, 90, 1500, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.2f, 375, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.5f, 400, float.MaxValue, false, SkillshotType.SkillshotCircle);

            var champMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var comboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(comboMenu, "Q", "Use Q");
                    AddItem(comboMenu, "QAir", "-> On Air");
                    AddItem(comboMenu, "E", "Use E");
                    AddItem(comboMenu, "EDmg", "-> Deal Damage");
                    AddItem(comboMenu, "EDmgRange", "--> If Enemy Not In", 250, 1, 475);
                    AddItem(comboMenu, "EGap", "-> Gap Closer");
                    AddItem(comboMenu, "EGapRange", "--> If Enemy Not In", 300, 1, 475);
                    AddItem(comboMenu, "R", "Use R");
                    AddItem(comboMenu, "RDelay", "-> Delay");
                    AddItem(comboMenu, "RDelayTime", "--> Time", 200, 150, 400);
                    AddItem(comboMenu, "RHpU", "-> If Enemy Hp Under", 50);
                    AddItem(comboMenu, "RCountA", "-> If Enemy Above", 2, 1, 5);
                    champMenu.AddSubMenu(comboMenu);
                }
                var harassMenu = new Menu("Harass", "Harass");
                {
                    AddItem(harassMenu, "AutoQ", "Auto Q", "H", KeyBindType.Toggle);
                    AddItem(harassMenu, "AutoQ3", "-> Use Q3");
                    AddItem(harassMenu, "AutoQTower", "-> Under Tower");
                    AddItem(harassMenu, "Q", "Use Q");
                    AddItem(harassMenu, "Q3", "-> Use Q3");
                    AddItem(harassMenu, "QTower", "-> Under Tower");
                    AddItem(harassMenu, "QLastHit", "-> Last Hit (Q1/Q2)");
                    AddItem(harassMenu, "E", "Use E");
                    AddItem(harassMenu, "ERange", "-> If Enemy Not In", 250, 1, 475);
                    AddItem(harassMenu, "ETower", "-> Under Tower");
                    champMenu.AddSubMenu(harassMenu);
                }
                var clearMenu = new Menu("Clear", "Clear");
                {
                    AddItem(clearMenu, "Q", "Use Q");
                    AddItem(clearMenu, "Q3", "-> Use Q3");
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
                    AddItem(miscMenu, "StackQ", "Auto Stack Q", "Z", KeyBindType.Toggle);
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
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Obj_AI_Base.OnPlayAnimation += OnPlayAnimation;
        }

        private bool HaveQ3
        {
            get { return Player.HasBuff("YasuoQ3W"); }
        }

        private float GetQDelay
        {
            get { return 1 / (1 / 0.5f * Player.AttackSpeedMod); }
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling())
            {
                return;
            }
            if (!Equals(Q.Delay, GetQDelay * 0.3f))
            {
                Q.SetSkillshot(GetQDelay * 0.3f, 55, 1500, false, SkillshotType.SkillshotLine);
                Q2.SetSkillshot(GetQDelay, 90, 1500, false, SkillshotType.SkillshotLine);
            }
            switch (Orbwalk.CurrentMode)
            {
                case Orbwalker.Mode.Combo:
                    Fight("Combo");
                    break;
                case Orbwalker.Mode.Harass:
                    Fight("Harass");
                    break;
                case Orbwalker.Mode.Clear:
                    Clear();
                    break;
                case Orbwalker.Mode.LastHit:
                    LastHit();
                    break;
                case Orbwalker.Mode.Flee:
                    Flee();
                    break;
            }
            AutoQ();
            KillSteal();
            StackQ();
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
                !GetValue<bool>("Interrupt", unit.ChampionName + "_" + spell.Slot) || !HaveQ3)
            {
                return;
            }
            if (E.IsReady() && (Q.IsReady() || Q.IsReady(150)))
            {
                if (E.IsInRange(unit) && CanCastE(unit) && E.WillHit(unit.ServerPosition, PosAfterE(unit)) &&
                    E.CastOnUnit(unit, PacketCast))
                {
                    return;
                }
                if (E.IsInRange(unit, E.Range + E.Width))
                {
                    var obj = GetNearObj(unit.ServerPosition, true);
                    if (obj != null && E.CastOnUnit(obj, PacketCast))
                    {
                        return;
                    }
                }
            }
            if (!Q.IsReady())
            {
                return;
            }
            if (_isDashing)
            {
                if (GetQCirObj(true).Count > 0)
                {
                    Q2.Cast(unit.ServerPosition, PacketCast);
                }
            }
            else
            {
                Q2.CastIfHitchanceEquals(unit, HitChance.High, PacketCast);
            }
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }
            if (GetValue<bool>("Combo", "Q") && GetValue<bool>("Combo", "QAir") &&
                args.SData.Name == "YasuoRKnockUpComboW")
            {
                Utility.DelayAction.Add(
                    ((int) (Q.Instance.CooldownExpires + 0.8) - Game.Ping / 1000) * 100,
                    () => Q.CastOnBestTarget(0, PacketCast));
            }
        }

        private void OnPlayAnimation(GameObject sender, GameObjectPlayAnimationEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }
            if (args.Animation == "Spell3")
            {
                _isDashing = true;
                Utility.DelayAction.Add(
                    (int) (475000 / (700 + Player.MoveSpeed)) + 30, () =>
                    {
                        if (_isDashing)
                        {
                            _isDashing = false;
                        }
                    });
            }
            else
            {
                _isDashing = false;
            }
        }

        private void Fight(string mode)
        {
            if (mode == "Combo" && GetValue<bool>(mode, "R") && R.IsReady())
            {
                var obj = HeroManager.Enemies.FindAll(CanCastR);
                var target = obj.Find(i => i.GetEnemiesInRange(R.Width).FindAll(CanCastR).Count > 1 && CanKill(i, R)) ??
                             obj.Find(
                                 i =>
                                     i.GetEnemiesInRange(R.Width).FindAll(CanCastR).Count > 1 &&
                                     i.GetEnemiesInRange(R.Width)
                                         .FindAll(CanCastR)
                                         .Count(a => a.HealthPercentage() < GetValue<Slider>(mode, "RHpU").Value) > 0) ??
                             obj.Find(
                                 i =>
                                     i.GetEnemiesInRange(R.Width).FindAll(CanCastR).Count >=
                                     GetValue<Slider>(mode, "RCountA").Value);
                if (target != null && (!GetValue<bool>(mode, "RDelay") || DelayR(target)) &&
                    R.Cast(target.ServerPosition, PacketCast))
                {
                    return;
                }
            }
            if (GetValue<bool>(mode, "E") && E.IsReady())
            {
                if (mode == "Combo" && GetValue<bool>(mode, "EGap"))
                {
                    var target = R.GetTarget();
                    if (target != null && Player.Distance(target) > GetValue<Slider>(mode, "EGapRange").Value)
                    {
                        var obj = GetNearObj(target.ServerPosition);
                        if (obj != null && E.CastOnUnit(obj, PacketCast))
                        {
                            return;
                        }
                    }
                }
                if ((mode == "Combo" && GetValue<bool>(mode, "EDmg")) ||
                    (mode == "Harass" && (!UnderTower(Player.ServerPosition) || GetValue<bool>(mode, "ETower"))))
                {
                    var target = E.GetTarget();
                    if (target != null &&
                        Player.Distance(target) >
                        GetValue<Slider>(mode, "E" + (mode == "Harass" ? "" : "Dmg") + "Range").Value)
                    {
                        var eBuff = Player.Buffs.Find(i => i.DisplayName == "YasuoDashScalar");
                        if (eBuff != null && eBuff.Count == 2 && CanCastE(target) && E.CastOnUnit(target, PacketCast))
                        {
                            return;
                        }
                        var obj = GetNearObj(target.ServerPosition);
                        if (GetValue<bool>(mode, "Q") && (Q.IsReady() || Q.IsReady(150)) &&
                            GetNearObj(target.ServerPosition, true) != null)
                        {
                            obj = GetNearObj(target.ServerPosition, true);
                        }
                        if (obj != null && E.CastOnUnit(obj, PacketCast))
                        {
                            return;
                        }
                    }
                }
            }
            if (GetValue<bool>(mode, "Q") && Q.IsReady())
            {
                if (mode == "Combo" ||
                    ((!HaveQ3 || GetValue<bool>(mode, "Q3")) &&
                     (!UnderTower(Player.ServerPosition) || GetValue<bool>(mode, "QTower"))))
                {
                    if (_isDashing)
                    {
                        if (GetQCirObj(true).Count > 0 && Q.Cast(Player.ServerPosition, PacketCast))
                        {
                            return;
                        }
                    }
                    else if ((HaveQ3 ? Q2 : Q).CastOnBestTarget(0, PacketCast).IsCasted())
                    {
                        return;
                    }
                }
                if (mode == "Harass" && GetValue<bool>(mode, "QLastHit") && Q.GetTarget(100) == null && !HaveQ3 &&
                    !_isDashing)
                {
                    var obj =
                        MinionManager.GetMinions(
                            Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                            .Cast<Obj_AI_Minion>()
                            .Find(i => CanKill(i, Q));
                    if (obj != null)
                    {
                        Q.CastIfHitchanceEquals(obj, HitChance.High, PacketCast);
                    }
                }
            }
        }

        private void Clear()
        {
            if (GetValue<bool>("Clear", "E") && E.IsReady())
            {
                var minionObj =
                    MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                        .FindAll(i => CanCastE(i) && (!UnderTower(PosAfterE(i)) || GetValue<bool>("Clear", "ETower")));
                if (minionObj.Count > 0)
                {
                    var obj = (Obj_AI_Base) minionObj.Cast<Obj_AI_Minion>().Find(i => CanKill(i, E, GetEDmg(i)));
                    if (obj == null && GetValue<bool>("Clear", "Q") && (Q.IsReady() || Q.IsReady(150)) &&
                        (!HaveQ3 || GetValue<bool>("Clear", "Q3")))
                    {
                        obj =
                            minionObj.FindAll(
                                i =>
                                    MinionManager.GetMinions(PosAfterE(i), E.Width, MinionTypes.All, MinionTeam.NotAlly)
                                        .Count > 1)
                                .MaxOrDefault(
                                    i =>
                                        MinionManager.GetMinions(
                                            PosAfterE(i), E.Width, MinionTypes.All, MinionTeam.NotAlly).Count);
                    }
                    if (obj != null && E.CastOnUnit(obj, PacketCast))
                    {
                        return;
                    }
                }
            }
            if (GetValue<bool>("Clear", "Q") && Q.IsReady() && (!HaveQ3 || GetValue<bool>("Clear", "Q3")))
            {
                if (_isDashing)
                {
                    if ((GetQCirObj(true).Count > 0 || GetQCirObj().Count > 1) &&
                        Q.Cast(Player.ServerPosition, PacketCast))
                    {
                        return;
                    }
                }
                else
                {
                    var minionObj = MinionManager.GetMinions(
                        (HaveQ3 ? Q2 : Q).Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
                    if (minionObj.Count > 0)
                    {
                        if (HaveQ3)
                        {
                            var pos = Q2.GetLineFarmLocation(minionObj);
                            if (pos.MinionsHit > 0 && Q2.Cast(pos.Position, PacketCast))
                            {
                                return;
                            }
                        }
                        else
                        {
                            var obj = minionObj.Cast<Obj_AI_Minion>().Find(i => CanKill(i, Q));
                            if (obj != null)
                            {
                                if (Q.CastIfHitchanceEquals(obj, HitChance.High, PacketCast))
                                {
                                    return;
                                }
                            }
                            else
                            {
                                var pos =
                                    Player.ServerPosition.To2D()
                                        .Closest(
                                            MinionManager.GetMinionsPredictedPositions(
                                                minionObj, Q.Delay, Q.Width, Q.Speed, Player.ServerPosition, Q.Range,
                                                false, SkillshotType.SkillshotLine));
                                if (Q.IsInRange(pos) && Q.Cast(pos, PacketCast))
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            if (GetValue<bool>("Clear", "Item") && (Hydra.IsReady() || Tiamat.IsReady()))
            {
                var minionObj = MinionManager.GetMinions(
                    (Hydra.IsReady() ? Hydra : Tiamat).Range, MinionTypes.All, MinionTeam.NotAlly);
                if (minionObj.Count > 2 ||
                    minionObj.Any(
                        i => i.MaxHealth >= 1200 && i.Distance(Player) < (Hydra.IsReady() ? Hydra : Tiamat).Range - 80))
                {
                    if (Tiamat.IsReady())
                    {
                        Tiamat.Cast();
                    }
                    if (Hydra.IsReady())
                    {
                        Hydra.Cast();
                    }
                }
            }
        }

        private void LastHit()
        {
            if (GetValue<bool>("LastHit", "Q") && Q.IsReady() && !_isDashing &&
                (!HaveQ3 || GetValue<bool>("LastHit", "Q3")))
            {
                var obj =
                    MinionManager.GetMinions(
                        (HaveQ3 ? Q2 : Q).Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                        .Cast<Obj_AI_Minion>()
                        .Find(i => CanKill(i, HaveQ3 ? Q2 : Q));
                if (obj != null && (HaveQ3 ? Q2 : Q).CastIfHitchanceEquals(obj, HitChance.High, PacketCast))
                {
                    return;
                }
            }
            if (GetValue<bool>("LastHit", "E") && E.IsReady())
            {
                var obj =
                    MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                        .Cast<Obj_AI_Minion>()
                        .FindAll(
                            i =>
                                CanCastE(i) &&
                                (!Orbwalk.InAutoAttackRange(i) || i.Health > Player.GetAutoAttackDamage(i, true)) &&
                                (!UnderTower(PosAfterE(i)) || GetValue<bool>("LastHit", "ETower")))
                        .Find(i => CanKill(i, E, GetEDmg(i)));
                if (obj != null)
                {
                    E.CastOnUnit(obj, PacketCast);
                }
            }
        }

        private void Flee()
        {
            if (!GetValue<bool>("Flee", "E"))
            {
                return;
            }
            if (GetValue<bool>("Flee", "EStackQ") && Q.IsReady() && !HaveQ3 && _isDashing && GetQCirObj().Count > 0 &&
                Q.Cast(Player.ServerPosition, PacketCast))
            {
                return;
            }
            var obj = GetNearObj(Game.CursorPos);
            if (obj == null || !E.IsReady())
            {
                return;
            }
            E.CastOnUnit(obj, PacketCast);
        }

        private void AutoQ()
        {
            if (!GetValue<KeyBind>("Harass", "AutoQ").Active || !Q.IsReady() || _isDashing ||
                (HaveQ3 && !GetValue<bool>("Harass", "AutoQ3")) ||
                (UnderTower(Player.ServerPosition) && !GetValue<bool>("Harass", "AutoQTower")))
            {
                return;
            }
            (HaveQ3 ? Q2 : Q).CastOnBestTarget(0, PacketCast);
        }

        private void KillSteal()
        {
            if (GetValue<bool>("KillSteal", "Ignite") && Ignite.IsReady())
            {
                var target = TargetSelector.GetTarget(600, TargetSelector.DamageType.True);
                if (target != null && CastIgnite(target))
                {
                    return;
                }
            }
            if (GetValue<bool>("KillSteal", "Q") && Q.IsReady())
            {
                if (_isDashing)
                {
                    if (GetQCirObj(true).Cast<Obj_AI_Hero>().Count(i => CanKill(i, Q)) > 0 &&
                        Q.Cast(Player.ServerPosition, PacketCast))
                    {
                        return;
                    }
                }
                else
                {
                    var target = (HaveQ3 ? Q2 : Q).GetTarget();
                    if (target != null && CanKill(target, HaveQ3 ? Q2 : Q) &&
                        (HaveQ3 ? Q2 : Q).CastIfHitchanceEquals(target, HitChance.High, PacketCast))
                    {
                        return;
                    }
                }
            }
            if (GetValue<bool>("KillSteal", "E") && E.IsReady())
            {
                var target = E.GetTarget(0, HeroManager.Enemies.FindAll(i => !CanCastE(i)));
                if (target != null && CanKill(target, E, GetEDmg(target)) && E.CastOnUnit(target, PacketCast))
                {
                    return;
                }
            }
            if (GetValue<bool>("KillSteal", "R") && R.IsReady())
            {
                var target = R.GetTarget(0, HeroManager.Enemies.FindAll(i => !CanCastR(i)));
                if (target != null && CanKill(target, R))
                {
                    R.CastOnUnit(target, PacketCast);
                }
            }
        }

        private void StackQ()
        {
            if (!GetValue<KeyBind>("Misc", "StackQ").Active || !Q.IsReady() || _isDashing || HaveQ3)
            {
                return;
            }
            var target = Q.GetTarget();
            if (target != null && !UnderTower(Player.ServerPosition))
            {
                Q.CastIfHitchanceEquals(target, HitChance.High, PacketCast);
            }
            else
            {
                var minionObj = MinionManager.GetMinions(
                    Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
                var obj = minionObj.Cast<Obj_AI_Minion>().Find(i => CanKill(i, Q)) ?? minionObj.FirstOrDefault();
                if (obj != null)
                {
                    Q.CastIfHitchanceEquals(obj, HitChance.High, PacketCast);
                }
            }
        }

        private bool CanCastE(Obj_AI_Base target)
        {
            return !target.HasBuff("YasuoDashWrapper");
        }

        private bool CanCastR(Obj_AI_Hero target)
        {
            return target.HasBuff("yasuoq3mis") || target.HasBuffOfType(BuffType.Knockup) ||
                   target.HasBuffOfType(BuffType.Knockback);
        }

        private bool DelayR(Obj_AI_Hero target)
        {
            var buff = target.Buffs.Find(i => i.Type == BuffType.Knockup) ??
                       target.Buffs.Find(i => i.Type == BuffType.Knockback);
            return buff != null &&
                   buff.EndTime - Game.Time < (float) GetValue<Slider>("Combo", "RDelayTime").Value / 1000;
        }

        private double GetEDmg(Obj_AI_Base target)
        {
            var eBuff = Player.Buffs.Find(i => i.DisplayName == "YasuoDashScalar");
            return Player.CalcDamage(
                target, Damage.DamageType.Magical,
                new[] { 70, 90, 110, 130, 150 }[E.Level - 1] * (1 + 0.25 * (eBuff != null ? eBuff.Count : 0)) +
                0.6 * Player.FlatMagicDamageMod);
        }

        private Obj_AI_Base GetNearObj(Vector3 pos, bool inQCir = false)
        {
            return
                MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly)
                    .FindAll(
                        i =>
                            CanCastE(i) &&
                            (!inQCir ? pos.Distance(PosAfterE(i)) < Player.Distance(pos) : E.WillHit(pos, PosAfterE(i))))
                    .MinOrDefault(i => pos.Distance(PosAfterE(i))) ??
                HeroManager.Enemies.FindAll(
                    i =>
                        i.IsValidTarget(E.Range) && CanCastE(i) &&
                        (!inQCir ? pos.Distance(PosAfterE(i)) < Player.Distance(pos) : E.WillHit(pos, PosAfterE(i))))
                    .MinOrDefault(i => pos.Distance(PosAfterE(i)));
        }

        private List<Obj_AI_Base> GetQCirObj(bool onlyHero = false)
        {
            var heroObj = HeroManager.Enemies.FindAll(i => i.IsValidTarget(E.Width)).Cast<Obj_AI_Base>().ToList();
            var minionObj = MinionManager.GetMinions(E.Width, MinionTypes.All, MinionTeam.NotAlly);
            return onlyHero ? heroObj : (heroObj.Count > 0 ? heroObj : minionObj);
        }

        private Vector3 PosAfterE(Obj_AI_Base target)
        {
            return Player.ServerPosition.Extend(target.ServerPosition, E.Range);
        }

        private bool UnderTower(Vector3 pos)
        {
            return ObjectManager.Get<Obj_AI_Turret>().Any(i => i.IsEnemy && !i.IsDead && i.Distance(pos) <= 850);
        }
    }
}
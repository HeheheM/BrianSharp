using System;
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
        public Yasuo()
        {
            Q = new Spell(SpellSlot.Q, 490);
            Q2 = new Spell(SpellSlot.Q, 1000);
            W = new Spell(SpellSlot.W, 400);
            E = new Spell(SpellSlot.E, 485, TargetSelector.DamageType.Magical);
            R = new Spell(SpellSlot.R, 1200);
            Q.SetSkillshot(0.5f, 55, 1500, false, SkillshotType.SkillshotLine);
            Q2.SetSkillshot(0.5f, 90, 1500, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.5f, 375, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.5f, 400, float.MaxValue, false, SkillshotType.SkillshotCircle);

            var champMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                //var comboMenu = new Menu("Combo", "Combo");
                //{
                //    AddItem(comboMenu, "Q", "Use Q");
                //    AddItem(comboMenu, "E", "Use E");
                //    AddItem(comboMenu, "EDmg", "-> Deal Damage");
                //    AddItem(comboMenu, "EGap", "-> Gap Closer");
                //    AddItem(comboMenu, "EGapRange", "--> If Enemy Not In", 300, 0, 475);
                //    AddItem(comboMenu, "R", "Use R");
                //    AddItem(comboMenu, "RKill", "-> If Killable");
                //    AddItem(comboMenu, "RHpU", "-> If Enemy Hp Under (0 = Off)", 50, 0);
                //    AddItem(comboMenu, "RCountA", "-> If Enemy Above (0 = Off)", 2, 0, 5);
                //    champMenu.AddSubMenu(comboMenu);
                //}
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
                    Fight();
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
            if (E.IsReady() && E.IsInRange(unit))
            {
                if (!HaveE(unit) && !TargetSelector.IsInvulnerable(unit, TargetSelector.DamageType.Magical) &&
                    E.WillHit(Player.ServerPosition, PosAfterE(unit)))
                {
                    if (E.CastOnUnit(unit, PacketCast))
                    {
                        Utility.DelayAction.Add(200, () => Q.Cast(unit.ServerPosition, PacketCast));
                    }
                }
                else
                {
                    var obj =
                        ObjectManager.Get<Obj_AI_Base>()
                            .FindAll(
                                i =>
                                    !(i is Obj_AI_Turret) && i.IsValidTarget(E.Range) &&
                                    !TargetSelector.IsInvulnerable(i, TargetSelector.DamageType.Magical) &&
                                    E.WillHit(unit.ServerPosition, PosAfterE(i)))
                            .MinOrDefault(i => unit.Distance(PosAfterE(i)));
                    if (obj != null)
                    {
                        if (E.CastOnUnit(obj, PacketCast))
                        {
                            Utility.DelayAction.Add(200, () => Q.Cast(unit.ServerPosition, PacketCast));
                        }
                    }
                    else
                    {
                        Q2.CastIfHitchanceEquals(unit, HitChance.High, PacketCast);
                    }
                }
            }
            else
            {
                Q2.CastIfHitchanceEquals(unit, HitChance.High, PacketCast);
            }
        }

        private void Fight() {}

        private void Clear()
        {
            var minionObjE =
                MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                    .FindAll(i => !HaveE(i) && (GetValue<bool>("Clear", "ETower") || !UnderTower(PosAfterE(i))));
            if (GetValue<bool>("Clear", "Q") && Q.IsReady() && GetValue<bool>("Clear", "E") && E.IsReady())
            {
                var obj =
                    minionObjE.MaxOrDefault(
                        i =>
                            MinionManager.GetMinions(
                                PosAfterE(i), E.Width, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                                .Count);
                if (obj != null && E.CastOnUnit(obj, PacketCast))
                {
                    Utility.DelayAction.Add(200, () => Q.Cast(obj.ServerPosition, PacketCast));
                    return;
                }
            }
            if (GetValue<bool>("Clear", "Q") && Q.IsReady())
            {
                var minionObj = MinionManager.GetMinions(
                    (HaveQ3 ? Q2 : Q).Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
                if (minionObj.Count > 0)
                {
                    var pos = (HaveQ3 ? Q2 : Q).GetLineFarmLocation(minionObj);
                    if (pos.MinionsHit > 0 && pos.Position.IsValid() && (HaveQ3 ? Q2 : Q).Cast(pos.Position, PacketCast))
                    {
                        return;
                    }
                }
            }
            if (GetValue<bool>("Clear", "E") && E.IsReady())
            {
                var obj = minionObjE.Cast<Obj_AI_Minion>().Find(i => CanKill(i, E, GetEDmg(i)));
                if (obj != null && E.CastOnUnit(obj, PacketCast))
                {
                    return;
                }
            }
            if (GetValue<bool>("Clear", "Item") && (Hydra.IsReady() || Tiamat.IsReady()))
            {
                var minionObj = MinionManager.GetMinions(
                    (Hydra.IsReady() ? Hydra : Tiamat).Range, MinionTypes.All, MinionTeam.NotAlly,
                    MinionOrderTypes.MaxHealth);
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
            if (GetValue<bool>("LastHit", "Q") && Q.IsReady() && (!HaveQ3 || GetValue<bool>("LastHit", "Q3")))
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
                        .FindAll(i => !HaveE(i) && (GetValue<bool>("LastHit", "ETower") || !UnderTower(PosAfterE(i))))
                        .Find(i => CanKill(i, E, GetEDmg(i)));
                if (obj != null)
                {
                    E.CastOnUnit(obj, PacketCast);
                }
            }
        }

        private void Flee()
        {
            if (!GetValue<bool>("Flee", "E") || !E.IsReady())
            {
                return;
            }
            var obj =
                MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly)
                    .FindAll(i => !HaveE(i) && Game.CursorPos.Distance(PosAfterE(i)) < Player.Distance(Game.CursorPos))
                    .MinOrDefault(i => Player.Distance(i)) ??
                HeroManager.Enemies.FindAll(
                    i =>
                        i.IsValidTarget(E.Range) && !HaveE(i) &&
                        !TargetSelector.IsInvulnerable(i, TargetSelector.DamageType.Magical) &&
                        Game.CursorPos.Distance(PosAfterE(i)) < Player.Distance(Game.CursorPos))
                    .MinOrDefault(i => Player.Distance(i));
            if (obj != null && E.CastOnUnit(obj, PacketCast))
            {
                if (GetValue<bool>("Flee", "EStackQ") && Q.IsReady() && !HaveQ3)
                {
                    Utility.DelayAction.Add(200, () => Q.Cast(obj.ServerPosition, PacketCast));
                }
            }
        }

        private void AutoQ()
        {
            if (!GetValue<KeyBind>("Harass", "Q").Active || !Q.IsReady() || (HaveQ3 && !GetValue<bool>("Harass", "Q3")))
            {
                return;
            }
            var target = (HaveQ3 ? Q2 : Q).GetTarget();
            if (target == null || (GetValue<bool>("Harass", "QTower") && UnderTower(target.ServerPosition) && !HaveQ3))
            {
                return;
            }
            (HaveQ3 ? Q2 : Q).CastIfHitchanceEquals(target, HitChance.High, PacketCast);
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
                var target = (HaveQ3 ? Q2 : Q).GetTarget();
                if (target != null && CanKill(target, HaveQ3 ? Q2 : Q))
                {
                    if (E.IsReady() && E.IsInRange(target))
                    {
                        if (!HaveE(target) && !TargetSelector.IsInvulnerable(target, TargetSelector.DamageType.Magical) &&
                            E.WillHit(Player.ServerPosition, PosAfterE(target)))
                        {
                            if (E.CastOnUnit(target, PacketCast))
                            {
                                Utility.DelayAction.Add(200, () => Q.Cast(target.ServerPosition, PacketCast));
                                return;
                            }
                        }
                        else
                        {
                            var obj =
                                ObjectManager.Get<Obj_AI_Base>()
                                    .FindAll(
                                        i =>
                                            !(i is Obj_AI_Turret) && i.IsValidTarget(E.Range) && !HaveE(i) &&
                                            !TargetSelector.IsInvulnerable(i, TargetSelector.DamageType.Magical) &&
                                            E.WillHit(target.ServerPosition, PosAfterE(i)))
                                    .MinOrDefault(i => target.Distance(PosAfterE(i)));
                            if (obj != null)
                            {
                                if (E.CastOnUnit(obj, PacketCast))
                                {
                                    Utility.DelayAction.Add(200, () => Q.Cast(obj.ServerPosition, PacketCast));
                                    return;
                                }
                            }
                            else if ((HaveQ3 ? Q2 : Q).CastIfHitchanceEquals(target, HitChance.High, PacketCast))
                            {
                                return;
                            }
                        }
                    }
                    else if ((HaveQ3 ? Q2 : Q).CastIfHitchanceEquals(target, HitChance.High, PacketCast))
                    {
                        return;
                    }
                }
            }
            if (GetValue<bool>("KillSteal", "E") && E.IsReady())
            {
                var target = E.GetTarget(0, HeroManager.Enemies.FindAll(HaveE));
                if (target != null && CanKill(target, E, GetEDmg(target)) && E.CastOnUnit(target, PacketCast))
                {
                    return;
                }
            }
            if (GetValue<bool>("KillSteal", "R") && R.IsReady())
            {
                var target = R.GetTarget(0, HeroManager.Enemies.FindAll(i => !HaveR(i)));
                if (target != null && CanKill(target, R))
                {
                    R.CastOnUnit(target, PacketCast);
                }
            }
        }

        private bool HaveE(Obj_AI_Base target)
        {
            return target.HasBuff("YasuoDashWrapper");
        }

        private bool HaveR(Obj_AI_Hero target)
        {
            return target.HasBuff("yasuoq3mis") || target.HasBuffOfType(BuffType.Knockup) ||
                   target.HasBuffOfType(BuffType.Knockback);
        }

        private float DelayR(Obj_AI_Hero target)
        {
            var buff = target.Buffs.Find(i => i.Type == BuffType.Knockup) ??
                       target.Buffs.Find(i => i.Type == BuffType.Knockback);
            return buff != null ? buff.EndTime - Game.Time : 0;
        }

        private double GetEDmg(Obj_AI_Base target)
        {
            var eBuff = Player.Buffs.Find(i => i.DisplayName == "YasuoDashScalar");
            return Player.CalcDamage(
                target, Damage.DamageType.Magical,
                new[] { 70, 90, 110, 130, 150 }[E.Level - 1] * (1 + 0.25 * (eBuff != null ? eBuff.Count : 0)) +
                0.6 * Player.FlatMagicDamageMod);
        }

        private Vector3 PosAfterE(Obj_AI_Base target)
        {
            return Player.ServerPosition.Extend(target.ServerPosition, E.Range);
        }

        private bool UnderTower(Vector3 pos)
        {
            return ObjectManager.Get<Obj_AI_Turret>().Any(i => i.IsEnemy && !i.IsDead && i.Distance(pos) <= 950);
        }
    }
}
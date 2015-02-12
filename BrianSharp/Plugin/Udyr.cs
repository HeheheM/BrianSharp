﻿using System;
using System.Linq;
using BrianSharp.Common;
using LeagueSharp;
using LeagueSharp.Common;
using Orbwalk = BrianSharp.Common.Orbwalker;

namespace BrianSharp.Plugin
{
    internal class Udyr : Helper
    {
        private int _aaCount;
        private bool _phoenixActive;

        public Udyr()
        {
            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 600);
            R = new Spell(SpellSlot.R);

            var champMenu = new Menu("Plugin", PlayerName + "_Plugin");
            {
                var comboMenu = new Menu("Combo", "Combo");
                {
                    AddItem(comboMenu, "Q", "Use Q");
                    AddItem(comboMenu, "W", "Use W");
                    AddItem(comboMenu, "WHpU", "-> If Hp Under", 70);
                    AddItem(comboMenu, "E", "Use E");
                    AddItem(comboMenu, "R", "Use R");
                    champMenu.AddSubMenu(comboMenu);
                }
                var clearMenu = new Menu("Clear", "Clear");
                {
                    AddSmiteMobMenu(clearMenu);
                    AddItem(clearMenu, "Q", "Use Q");
                    AddItem(clearMenu, "W", "Use W");
                    AddItem(clearMenu, "WHpU", "-> If Hp Under", 70);
                    AddItem(clearMenu, "R", "Use R");
                    AddItem(clearMenu, "Item", "Use Tiamat/Hydra Item");
                    champMenu.AddSubMenu(clearMenu);
                }
                var fleeMenu = new Menu("Flee", "Flee");
                {
                    AddItem(fleeMenu, "E", "Use E");
                    AddItem(fleeMenu, "Stack", "-> Passive Stack");
                    champMenu.AddSubMenu(fleeMenu);
                }
                var miscMenu = new Menu("Misc", "Misc");
                {
                    var killStealMenu = new Menu("Kill Steal", "KillSteal");
                    {
                        AddItem(killStealMenu, "Ignite", "Use Ignite");
                        AddItem(killStealMenu, "Smite", "Use Smite");
                        miscMenu.AddSubMenu(killStealMenu);
                    }
                    AddItem(miscMenu, "StunCycle", "Stun Cycle", "Z");
                    champMenu.AddSubMenu(miscMenu);
                }
                MainMenu.AddSubMenu(champMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Spellbook.OnCastSpell += OnCastSpell;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private Stance CurStance
        {
            get
            {
                switch (
                    Player.Buffs.Find(i => i.DisplayName.Contains("Udyr") && i.DisplayName.Contains("Stance"))
                        .DisplayName)
                {
                    case "UdyrTigerStance":
                        return Stance.Tiger;
                    case "UdyrTurtleStance":
                        return Stance.Turtle;
                    case "UdyrBearStance":
                        return Stance.Bear;
                    case "UdyrPhoenixStance":
                        return Stance.Phoenix;
                }
                return Stance.None;
            }
        }

        private void OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (!sender.Owner.IsMe)
            {
                return;
            }
            if (args.Slot == SpellSlot.Q || args.Slot == SpellSlot.W || args.Slot == SpellSlot.E ||
                args.Slot == SpellSlot.R)
            {
                _aaCount = 0;
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling())
            {
                if (Player.IsDead)
                {
                    _aaCount = 0;
                }
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
                case Orbwalker.Mode.Flee:
                    Flee();
                    break;
            }
            if (GetValue<KeyBind>("Misc", "StunCycle").Active)
            {
                StunCycle();
            }
            KillSteal();
        }

        private void AfterAttack(AttackableUnit target)
        {
            if ((Orbwalk.CurrentMode != Orbwalker.Mode.Combo && Orbwalk.CurrentMode != Orbwalker.Mode.Clear) ||
                (CurStance != Stance.Tiger && CurStance != Stance.Phoenix && CurStance != Stance.Turtle))
            {
                return;
            }
            _aaCount += 1;
            if (CurStance == Stance.Phoenix && Player.Buffs.Find(i => i.DisplayName == "UdyrPhoenixStance").Count == 1)
            {
                _phoenixActive = true;
                Utility.DelayAction.Add(50, () => _phoenixActive = false);
            }
        }

        private void Fight()
        {
            var target = E.GetTarget();
            if (target == null)
            {
                return;
            }
            if (GetValue<bool>("Combo", "E") && E.IsReady() && !target.HasBuff("UdyrBearStunCheck") &&
                E.Cast(PacketCast))
            {
                return;
            }
            if ((GetValue<bool>("Combo", "E") && E.Level > 0 && !target.HasBuff("UdyrBearStunCheck")) ||
                !Orbwalk.InAutoAttackRange(target, 30) ||
                (GetValue<bool>("Combo", "W") && CurStance == Stance.Turtle && _aaCount < 2))
            {
                return;
            }
            if (GetValue<bool>("Combo", "W") && W.IsReady() &&
                Player.HealthPercentage() < GetValue<Slider>("Combo", "WHpU").Value &&
                ((CurStance == Stance.Tiger && _aaCount > 1) ||
                 (CurStance == Stance.Phoenix && (_aaCount > 3 || _phoenixActive)) || (Q.Level == 0 && R.Level == 0)) &&
                W.Cast(PacketCast))
            {
                return;
            }
            if (GetValue<bool>("Combo", "Q") && Q.Cast(PacketCast))
            {
                return;
            }
            if (GetValue<bool>("Combo", "R") && R.IsReady() &&
                (!GetValue<bool>("Combo", "Q") || Q.Level == 0 || (CurStance == Stance.Tiger && _aaCount > 1)))
            {
                R.Cast(PacketCast);
            }
        }

        private void Clear()
        {
            SmiteMob();
            var target = Orbwalk.GetPossibleTarget();
            if (target == null || (GetValue<bool>("Clear", "W") && CurStance == Stance.Turtle && _aaCount < 2))
            {
                return;
            }
            if (GetValue<bool>("Clear", "W") && W.IsReady() &&
                Player.HealthPercentage() < GetValue<Slider>("Clear", "WHpU").Value &&
                ((CurStance == Stance.Tiger && _aaCount > 2) ||
                 (CurStance == Stance.Phoenix && (_aaCount > 3 || _phoenixActive)) || (Q.Level == 0 && R.Level == 0)) &&
                W.Cast(PacketCast))
            {
                return;
            }
            if (GetValue<bool>("Clear", "Q") && Q.Cast(PacketCast))
            {
                return;
            }
            if (GetValue<bool>("Clear", "R") && R.IsReady() &&
                (!GetValue<bool>("Clear", "Q") || Q.Level == 0 || (CurStance == Stance.Tiger && _aaCount > 2)) &&
                R.Cast(PacketCast))
            {
                return;
            }
            if (GetValue<bool>("Clear", "Item"))
            {
                var item = Hydra.IsReady() ? Hydra : Tiamat;
                if (item.IsReady())
                {
                    var minionObj = MinionManager.GetMinions(
                        item.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
                    if (minionObj.Count > 2 ||
                        minionObj.Any(i => i.MaxHealth >= 1200 && i.Distance(Player) < item.Range - 80))
                    {
                        item.Cast();
                    }
                }
            }
        }

        private void Flee()
        {
            if (!GetValue<bool>("Flee", "E") || E.Cast(PacketCast) || !GetValue<bool>("Flee", "Stack"))
            {
                return;
            }
            var passive = Player.Buffs.Find(i => i.DisplayName == "UdyrMonkeyAgilityBuff");
            if (passive == null || passive.Count == 3)
            {
                return;
            }
            if (Q.IsReady() &&
                ((Q.Level > W.Level && Q.Level > R.Level) || (Q.Level == W.Level && Q.Level > R.Level) ||
                 (Q.Level == R.Level && Q.Level > W.Level) || (Q.Level == W.Level && Q.Level == R.Level)) &&
                Q.Cast(PacketCast))
            {
                return;
            }
            if (W.IsReady() &&
                ((W.Level > Q.Level && W.Level > R.Level) || (W.Level == Q.Level && W.Level > R.Level) ||
                 (W.Level == R.Level && W.Level > Q.Level) || (W.Level == Q.Level && W.Level == R.Level)) &&
                W.Cast(PacketCast))
            {
                return;
            }
            if (R.IsReady() &&
                ((R.Level > Q.Level && R.Level > W.Level) || (R.Level == Q.Level && R.Level > W.Level) ||
                 (R.Level == W.Level && R.Level > Q.Level) || (R.Level == Q.Level && R.Level == W.Level)))
            {
                R.Cast(PacketCast);
            }
        }

        private void StunCycle()
        {
            var obj =
                HeroManager.Enemies.FindAll(i => i.IsValidTarget(E.Range) && !i.HasBuff("UdyrBearStunCheck"))
                    .MinOrDefault(i => i.Distance(Player));
            CustomOrbwalk(null);
            if (obj == null)
            {
                return;
            }
            if (E.IsReady())
            {
                E.Cast(PacketCast);
            }
            if (CurStance != Stance.Bear)
            {
                return;
            }
            Player.IssueOrder(GameObjectOrder.AttackUnit, obj);
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
            if (GetValue<bool>("KillSteal", "Smite") &&
                (CurrentSmiteType == SmiteType.Blue || CurrentSmiteType == SmiteType.Red))
            {
                var target = TargetSelector.GetTarget(760, TargetSelector.DamageType.True);
                if (target != null)
                {
                    CastSmite(target);
                }
            }
        }

        private enum Stance
        {
            Tiger,
            Turtle,
            Bear,
            Phoenix,
            None
        }
    }
}
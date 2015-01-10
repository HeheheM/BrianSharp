using System;
using System.Linq;
using System.Collections.Generic;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace BrianSharp.Common
{
    public class Helper : Program
    {
        public static bool PacketCast
        {
            get { return ItemBool("Misc", "UsePacket"); }
        }

        public static void CustomOrbwalk(Obj_AI_Base Target)
        {
            Orbwalker.Orbwalk(Game.CursorPos, Orbwalker.InAutoAttackRange(Target) ? Target : null);
        }

        public static bool CanKill(Obj_AI_Base Target, Spell Skill, double Health, double SubDmg)
        {
            return Skill.GetHealthPrediction(Target) - Health + 5 <= SubDmg;
        }

        public static bool CanKill(Obj_AI_Base Target, Spell Skill, double SubDmg)
        {
            return CanKill(Target, Skill, 0, SubDmg);
        }

        public static bool CanKill(Obj_AI_Base Target, Spell Skill, int Stage = 0, double SubDmg = 0)
        {
            return Skill.GetHealthPrediction(Target) + 5 <= (SubDmg > 0 ? SubDmg : Skill.GetDamage(Target, Stage));
        }

        public static bool CastFlash(Vector3 Pos)
        {
            if (!Flash.IsReady() || !Pos.IsValid()) return false;
            return Player.Spellbook.CastSpell(Flash, Pos);
        }

        public static bool CastSmite(Obj_AI_Base Target, bool Killable = true)
        {
            if (!Smite.IsReady() || !Target.IsValidTarget(760) || (Killable && Target.Health > Player.GetSummonerSpellDamage(Target, Damage.SummonerSpell.Smite))) return false;
            return Player.Spellbook.CastSpell(Smite, Target);
        }

        public static bool CastIgnite(Obj_AI_Hero Target)
        {
            if (!Ignite.IsReady() || !Target.IsValidTarget(600) || Target.Health + 5 > Player.GetSummonerSpellDamage(Target, Damage.SummonerSpell.Ignite)) return false;
            return Player.Spellbook.CastSpell(Ignite, Target);
        }

        public static InventorySlot GetWardSlot()
        {
            InventorySlot Ward = null;
            int[] WardPink = { 3362, 2043 };
            int[] WardGreen = { 3340, 3361, 2049, 2045, 2044 };
            if (ItemBool("Misc", "WJPink")) Ward = Player.InventoryItems.FirstOrDefault(i => i.Id == (ItemId)WardPink.FirstOrDefault(a => Items.CanUseItem(a)));
            foreach (var Id in WardGreen.Where(i => Items.CanUseItem(i))) Ward = Player.InventoryItems.First(i => i.Id == (ItemId)Id);
            return Ward;
        }

        public static float GetWardRange()
        {
            int[] TricketWard = { 3340, 3361, 3362 };
            return 600 * ((Player.Masteries.Any(i => i.Page == MasteryPage.Utility && i.Id == 68 && i.Points == 1) && GetWardSlot() != null && TricketWard.Contains((int)GetWardSlot().Id)) ? 1.15f : 1);
        }

        public static bool CanSmiteMob(string Name)
        {
            if (!Smite.IsReady() || Name.Contains("Mini")) return false;
            if (ItemBool("SmiteMob", "Baron") && Name.StartsWith("SRU_Baron")) return true;
            if (ItemBool("SmiteMob", "Dragon") && Name.StartsWith("SRU_Dragon")) return true;
            if (ItemBool("SmiteMob", "Red") && Name.StartsWith("SRU_Red")) return true;
            if (ItemBool("SmiteMob", "Blue") && Name.StartsWith("SRU_Blue")) return true;
            if (ItemBool("SmiteMob", "Krug") && Name.StartsWith("SRU_Krug")) return true;
            if (ItemBool("SmiteMob", "Gromp") && Name.StartsWith("SRU_Gromp")) return true;
            if (ItemBool("SmiteMob", "Raptor") && Name.StartsWith("SRU_Razorbeak")) return true;
            if (ItemBool("SmiteMob", "Wolf") && Name.StartsWith("SRU_Murkwolf")) return true;
            return false;
        }

        public static void CastSkillShotSmite(Spell Skill, Obj_AI_Hero Target)
        {
            var Pred = Skill.GetPrediction(Target);
            if (ItemBool("Misc", "SmiteCol") && Pred.CollisionObjects.Count == 1 && Q.MinHitChance == HitChance.High && CastSmite(Pred.CollisionObjects.First()))
            {
                Q.Cast(Pred.CastPosition, PacketCast);
            }
            else Q.CastIfHitchanceEquals(Target, HitChance.VeryHigh, PacketCast);
        }
    }
}
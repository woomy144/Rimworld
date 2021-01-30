using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Verse.Sound;
using Verse;

namespace RimWorld
{

public class Bullet : Projectile
{
	protected override void Impact(Thing hitThing)
	{
		var map = Map; // before Impact!

		base.Impact(hitThing);
		
		var impact = new BattleLogEntry_RangedImpact(launcher, hitThing, intendedTarget.Thing, equipmentDef, def, targetCoverDef);
		Find.BattleLog.Add(impact);

		if( hitThing != null )
		{
			var dinfo = new DamageInfo(def.projectile.damageDef, DamageAmount, armorPenetration: ArmorPenetration, angle: ExactRotation.eulerAngles.y, instigator: launcher, weapon: equipmentDef, intendedTarget: intendedTarget.Thing);
			hitThing.TakeDamage(dinfo).AssociateWithLog(impact);

			var hitPawn = hitThing as Pawn;
			if( hitPawn != null && hitPawn.stances != null && hitPawn.BodySize <= def.projectile.StoppingPower + 0.001f )
				hitPawn.stances.StaggerFor(Pawn_StanceTracker.StaggerBulletImpactTicks);
		}
		else
		{
			SoundDefOf.BulletImpact_Ground.PlayOneShot(new TargetInfo(Position, map));
			MoteMaker.MakeStaticMote(ExactPosition, map, ThingDefOf.Mote_ShotHit_Dirt);

			if( Position.GetTerrain(map).takeSplashes )
				MoteMaker.MakeWaterSplash(ExactPosition, map, Mathf.Sqrt(DamageAmount) * MoteSplash.SizeGunfire, MoteSplash.VelocityGunfire);
		}
	}
}
}
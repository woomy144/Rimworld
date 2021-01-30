using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.IO;
using Verse;



namespace RimWorld{
public class Apparel : ThingWithComps
{
	//Working vars
	private bool wornByCorpseInt;

	//Properties
	public Pawn Wearer
	{
		get
		{
			var apparelTracker = ParentHolder as Pawn_ApparelTracker;
			return apparelTracker != null ? apparelTracker.pawn : null;
		}
	}
	public bool WornByCorpse { get { return wornByCorpseInt; } }
	public override string DescriptionDetailed
	{
		get
		{
			string descr = base.DescriptionDetailed;
			if( WornByCorpse )
				descr += "\n" + "WasWornByCorpse".Translate();
			
			return descr;
		}
	}

	public void Notify_PawnKilled()
	{
		if( def.apparel.careIfWornByCorpse )
			wornByCorpseInt = true;
	}

	public void Notify_PawnResurrected()
	{
		wornByCorpseInt = false;
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look( ref wornByCorpseInt, "wornByCorpse" );
	}

	public virtual void DrawWornExtras()
	{
	}

	public virtual bool CheckPreAbsorbDamage(DamageInfo dinfo)
	{
		return false;
	}

	public virtual bool AllowVerbCast(IntVec3 root, Map map, LocalTargetInfo targ, Verb verb)
	{
		return true;
	}

	public virtual IEnumerable<Gizmo> GetWornGizmos()
	{
		yield break;
	}

	public override string GetInspectString()
	{
		var s = base.GetInspectString();

		if( WornByCorpse )
		{
			if( s.Length > 0 )
				s += "\n";

			s += "WasWornByCorpse".Translate();
		}

		return s;
	}

	public virtual float GetSpecialApparelScoreOffset()
	{
		return 0;
	}
}
}

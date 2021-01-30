using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Verse.AI{
public class JobDriver_CastVerbOnce : JobDriver
{
	public override string GetReport()
	{
		string targetLabel;
		if( TargetA.HasThing )
			targetLabel = TargetThingA.LabelCap;
		else
			targetLabel = "AreaLower".Translate();

		return "UsingVerb".Translate(job.verbToUse.verbProps.label, targetLabel);
	}
	
	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return true;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDespawnedOrNull(TargetIndex.A);

		yield return Toils_Combat.GotoCastPosition( TargetIndex.A );

		yield return Toils_Combat.CastVerb( TargetIndex.A );
	}

}}


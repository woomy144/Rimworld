using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Verse.AI{
public class JobDriver_Kill : JobDriver
{
	//Constants
	private const TargetIndex VictimInd = TargetIndex.A;

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return pawn.Reserve(job.GetTarget(VictimInd), job, errorOnFailed: errorOnFailed);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.EndOnDespawnedOrNull(VictimInd, JobCondition.Succeeded);

		yield return Toils_Combat.TrySetJobToUseAttackVerb(VictimInd);

		Toil gotoCastPos = Toils_Combat.GotoCastPosition( VictimInd, maxRangeFactor: 0.95f ); // maxRangeFactor to prevent dithering
		yield return gotoCastPos;

		Toil jumpIfCannotHit = Toils_Jump.JumpIfTargetNotHittable( VictimInd, gotoCastPos );
		yield return jumpIfCannotHit;

		yield return Toils_Combat.CastVerb( VictimInd );

		yield return Toils_Jump.Jump( jumpIfCannotHit );
	}
}}

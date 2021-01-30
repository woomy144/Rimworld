using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;



namespace Verse.AI{
public class JobDriver_DoBill : JobDriver
{
	public float workLeft;
	public int billStartTick;
	public int ticksSpentDoingRecipeWork;
	public const PathEndMode GotoIngredientPathEndMode = PathEndMode.ClosestTouch;

	public const TargetIndex BillGiverInd = TargetIndex.A;
	public const TargetIndex IngredientInd = TargetIndex.B;
	public const TargetIndex IngredientPlaceCellInd = TargetIndex.C;
	
	public override string GetReport()
	{
		if( job.RecipeDef != null )
			return ReportStringProcessed(job.RecipeDef.jobString);
		else
			return base.GetReport();
	}
    
    public IBillGiver BillGiver
    {
        get
        {
            IBillGiver giver = job.GetTarget(BillGiverInd).Thing as IBillGiver;

            if(giver == null)
				throw new InvalidOperationException("DoBill on non-Billgiver.");

            return giver;
        }
    }

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref workLeft, "workLeft");
		Scribe_Values.Look(ref billStartTick, "billStartTick");
		Scribe_Values.Look(ref ticksSpentDoingRecipeWork, "ticksSpentDoingRecipeWork");
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		if( !pawn.Reserve(job.GetTarget(BillGiverInd), job, errorOnFailed: errorOnFailed) )
			return false;

		pawn.ReserveAsManyAsPossible(job.GetTargetQueue(IngredientInd), job);

		return true;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		//Bill giver destroyed (only in bill using phase! Not in carry phase)
		this.AddEndCondition( ()=>
			{
				var targ = this.GetActor().jobs.curJob.GetTarget(TargetIndex.A).Thing;
				if( targ is Building && !targ.Spawned)
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});

		this.FailOnBurningImmobile( TargetIndex.A );	//Bill giver, or product burning in carry phase

		this.FailOn( ()=>
		{
			IBillGiver billGiver = job.GetTarget(BillGiverInd).Thing as IBillGiver;

			//conditions only apply during the billgiver-use phase
			if( billGiver != null )
			{
				if( job.bill.DeletedOrDereferenced )
					return true;

				if( !billGiver.CurrentlyUsableForBills() )
					return true;
			}

			return false;
		});
		
        //This toil is yielded later
		Toil gotoBillGiver = Toils_Goto.GotoThing( BillGiverInd, PathEndMode.InteractionCell );

		//Bind to bill if it should
		Toil bind = new Toil();
		bind.initAction = ()=>
			{
				if( job.targetQueueB != null && job.targetQueueB.Count == 1 )
				{
					UnfinishedThing uft = job.targetQueueB[0].Thing as UnfinishedThing;
					if( uft != null )
						uft.BoundBill = (Bill_ProductionWithUft)job.bill;
				}
			};
		yield return bind;

		//Jump over ingredient gathering if there are no ingredients needed 
		yield return Toils_Jump.JumpIf( gotoBillGiver, ()=> job.GetTargetQueue(IngredientInd).NullOrEmpty() );

		//Gather ingredients
		{
    		//Extract an ingredient into IngredientInd target
    		Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue( IngredientInd );
    		yield return extract;
    
    		//Get to ingredient and pick it up
    		//Note that these fail cases must be on these toils, otherwise the recipe work fails if you stacked
    		//   your targetB into another object on the bill giver square.
			var getToHaulTarget = Toils_Goto.GotoThing(IngredientInd, GotoIngredientPathEndMode)
    								.FailOnDespawnedNullOrForbidden( IngredientInd )
									.FailOnSomeonePhysicallyInteracting( IngredientInd );
    		yield return getToHaulTarget;
    
    		yield return Toils_Haul.StartCarryThing( IngredientInd, putRemainderInQueue: true, failIfStackCountLessThanJobCount: true );
    
			//Jump to pick up more in this run if we're collecting from multiple stacks at once
    		yield return JumpToCollectNextIntoHandsForBill( getToHaulTarget, TargetIndex.B );
    
    		//Carry ingredient to the bill giver
    		yield return Toils_Goto.GotoThing( BillGiverInd, PathEndMode.InteractionCell )
    								.FailOnDestroyedOrNull( IngredientInd );
    
			//Place ingredient on the appropriate cell
			Toil findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell( BillGiverInd, IngredientInd, IngredientPlaceCellInd );
    		yield return findPlaceTarget;
    		yield return Toils_Haul.PlaceHauledThingInCell( IngredientPlaceCellInd,
															nextToilOnPlaceFailOrIncomplete: findPlaceTarget,
															storageMode: false );
    
    		//Jump back if another ingredient is queued, or you didn't finish carrying your current ingredient target
    		yield return Toils_Jump.JumpIfHaveTargetInQueue( IngredientInd, extract );
		}

        //For it no ingredients needed, just go to the bill giver
		//This will do nothing if we took ingredients and are thus already at the bill giver
		yield return gotoBillGiver;

		//If the recipe calls for the use of an UnfinishedThing
		//Create that and convert our job to be a job about working on it
		yield return Toils_Recipe.MakeUnfinishedThingIfNeeded();

		//Do the recipe
		//This puts the first product (if any) in targetC
        yield return Toils_Recipe.DoRecipeWork()
								 .FailOnDespawnedNullOrForbiddenPlacedThings()
								 .FailOnCannotTouch(BillGiverInd, PathEndMode.InteractionCell);
		
		//Finish doing this recipe
		//Generate the products
		//Modify the job to store them
		yield return Toils_Recipe.FinishRecipeAndStartStoringProduct();
        
		//If recipe has any products, store the first one
        if( !job.RecipeDef.products.NullOrEmpty() || !job.RecipeDef.specialProducts.NullOrEmpty() ) 
        {
    		//Reserve the storage cell
    		yield return Toils_Reserve.Reserve( TargetIndex.B );
    
    		Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
    		yield return carryToCell;
    
    		yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, storageMode: true);
    
    		//Bit of a hack here
    		//This makes the worker use a count including the one they just dropped
    		//When determining whether to make the next item if the bill has "make until you have" marked.
    		Toil recount = new Toil();
    		recount.initAction = ()=>
    			{
                    Bill_Production bill = recount.actor.jobs.curJob.bill as Bill_Production;
    				if( bill != null && bill.repeatMode == BillRepeatModeDefOf.TargetCount )
    					Map.resourceCounter.UpdateResourceCounts();
    			};
    		yield return recount;
        }
	}

	private static Toil JumpToCollectNextIntoHandsForBill( Toil gotoGetTargetToil, TargetIndex ind )
	{
		const float MaxDist = 8;

		Toil toil = new Toil();
		toil.initAction = ()=>
		{
			Pawn actor = toil.actor;

			if( actor.carryTracker.CarriedThing == null )
			{
				Log.Error("JumpToAlsoCollectTargetInQueue run on " + actor + " who is not carrying something.");
				return;
			}

			//Early-out
			if( actor.carryTracker.Full )
				return;

			Job curJob = actor.jobs.curJob;
			var targetQueue = curJob.GetTargetQueue(ind);

			if( targetQueue.NullOrEmpty() )
				return;

			//Find an item in the queue matching what you're carrying
			for( int i=0; i<targetQueue.Count; i++ )
			{
				//Can't use item - skip
				if( !GenAI.CanUseItemForWork( actor, targetQueue[i].Thing ) )
					continue;

				//Cannot stack with thing in hands - skip
				if( !targetQueue[i].Thing.CanStackWith(actor.carryTracker.CarriedThing) )
					continue;

				//Too far away - skip
				if( (actor.Position - targetQueue[i].Thing.Position).LengthHorizontalSquared > MaxDist*MaxDist )
					continue;

				//Determine num in hands
				int numInHands = (actor.carryTracker.CarriedThing==null) ? 0 : actor.carryTracker.CarriedThing.stackCount;

				//Determine num to take
				int numToTake = curJob.countQueue[i];
				numToTake = Mathf.Min(numToTake, targetQueue[i].Thing.def.stackLimit - numInHands);
				numToTake = Mathf.Min(numToTake, actor.carryTracker.AvailableStackSpace(targetQueue[i].Thing.def));

				//Won't take any - skip
				if( numToTake <= 0 )
					continue;

				//Set me to go get it
				curJob.count = numToTake;
				curJob.SetTarget( ind, targetQueue[i].Thing );

				//Remove the amount to take from the num to bring list
				//Remove from queue if I'm going to take all
				curJob.countQueue[i] -= numToTake;
				if( curJob.countQueue[i] <= 0 )
				{
					curJob.countQueue.RemoveAt(i);
					targetQueue.RemoveAt(i);
				}

				//Jump to toil
				actor.jobs.curDriver.JumpToToil( gotoGetTargetToil );
				return;
			}

		};

		return toil;
	}
}}

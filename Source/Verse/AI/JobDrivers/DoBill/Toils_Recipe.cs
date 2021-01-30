using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;


namespace Verse.AI{
public static class Toils_Recipe
{
	private const int	LongCraftingProjectThreshold = 10000;

	public static Toil MakeUnfinishedThingIfNeeded()
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
			{
				Pawn actor = toil.actor;
				Job curJob = actor.jobs.curJob;

				//Recipe doesn't use unfinished things
				if( !curJob.RecipeDef.UsesUnfinishedThing )
					return;

				//Already working on an unfinished thing
				if( curJob.GetTarget( JobDriver_DoBill.IngredientInd ).Thing is UnfinishedThing )
					return;

				//Create the unfinished thing
				var ingredients = CalculateIngredients(curJob, actor);
				Thing dominantIngredient = CalculateDominantIngredient(curJob, ingredients);

				//Despawn ingredients. They will be saved inside the UFT
				//because these ingredients can come back out if the UFT is canceled
				for( int i=0; i<ingredients.Count; i++ )
				{
					var ingredient = ingredients[i];
					actor.Map.designationManager.RemoveAllDesignationsOn(ingredient);
					if( ingredient.Spawned )
						ingredient.DeSpawn();
				}

				//Store the dominant ingredient as the UnfinishedThing's stuff
				ThingDef stuff = curJob.RecipeDef.unfinishedThingDef.MadeFromStuff
					? dominantIngredient.def
					: null;

				//Make the UFT and set its data
				UnfinishedThing uft = (UnfinishedThing)ThingMaker.MakeThing(curJob.RecipeDef.unfinishedThingDef, stuff);
				uft.Creator = actor;
				uft.BoundBill = (Bill_ProductionWithUft)curJob.bill;
				uft.ingredients = ingredients;
				CompColorable cc = uft.TryGetComp<CompColorable>();
				if( cc != null )
					cc.Color = dominantIngredient.DrawColor;

				//Spawn the UFT
				GenSpawn.Spawn(uft, curJob.GetTarget( JobDriver_DoBill.BillGiverInd ).Cell, actor.Map );

				//Set the job to use the unfinished thing as its only ingredient
				curJob.SetTarget( JobDriver_DoBill.IngredientInd, uft);

				//Reserve the unfinished thing
				actor.Reserve( uft, curJob );
			};
		return toil;
	}

	public static Toil DoRecipeWork()
	{
		const int MinWorkDuration = 3000;
		const int CheckOverrideInterval = 1000;

		Toil toil = new Toil();
		toil.initAction = ()=>
			{
				Pawn actor = toil.actor;
				Job curJob = actor.jobs.curJob;
				JobDriver_DoBill driver = ((JobDriver_DoBill)actor.jobs.curDriver);
				UnfinishedThing uft = curJob.GetTarget(JobDriver_DoBill.IngredientInd).Thing as UnfinishedThing;

				//Set our work left
				//If we're starting from an already-initialized UnfinishedThing, just copy its workLeft into the driver
				//Otherwise, generate a new workLeft and copy it into the UnfinishedThing
				if( uft != null && uft.Initialized)
				{
					driver.workLeft = uft.workLeft;
				}
				else
				{
					driver.workLeft = curJob.bill.recipe.WorkAmountTotal( uft != null ? uft.Stuff : null );

					if( uft != null )
						uft.workLeft = driver.workLeft;
				}

				driver.billStartTick = Find.TickManager.TicksGame;
				driver.ticksSpentDoingRecipeWork = 0;

				curJob.bill.Notify_DoBillStarted(actor);
			};
		toil.tickAction = ()=>
			{
				Pawn actor = toil.actor;
				Job curJob = actor.jobs.curJob;
				JobDriver_DoBill driver = ((JobDriver_DoBill)actor.jobs.curDriver);
				UnfinishedThing uft = curJob.GetTarget(JobDriver_DoBill.IngredientInd).Thing as UnfinishedThing;

				if( uft != null && uft.Destroyed )
				{
					actor.jobs.EndCurrentJob(JobCondition.Incompletable);
					return;
				}

				driver.ticksSpentDoingRecipeWork++;

				curJob.bill.Notify_PawnDidWork(actor);

				//Bill giver gets notification that we're working on it
				var bga = toil.actor.CurJob.GetTarget(JobDriver_DoBill.BillGiverInd).Thing as IBillGiverWithTickAction;
				if( bga != null )
					bga.UsedThisTick();

				//Learn (only if the recipe uses unfinished thing to prevent the exploit with drafting/undrafting colonists and getting unlimited xp) 
				if( curJob.RecipeDef.workSkill != null && curJob.RecipeDef.UsesUnfinishedThing )
					actor.skills.Learn(curJob.RecipeDef.workSkill, SkillTuning.XpPerTickRecipeBase * curJob.RecipeDef.workSkillLearnFactor );

				//Make some progress
				//Apply it to both the driver's workLeft and, if it exists, the UnfinishedThing's workLeft
				float progress = (curJob.RecipeDef.workSpeedStat==null) ? 1f : actor.GetStatValue( curJob.RecipeDef.workSpeedStat );

				if( curJob.RecipeDef.workTableSpeedStat != null )
				{
					var t = driver.BillGiver as Building_WorkTable;
					if( t != null )
						progress *= t.GetStatValue(curJob.RecipeDef.workTableSpeedStat);
				}

				if( DebugSettings.fastCrafting )
					progress *= 30;

				driver.workLeft -= progress;
				if( uft != null )
					uft.workLeft = driver.workLeft;

				PawnUtility.GainComfortFromCellIfPossible(actor);

				//End the toil if there is no more work left
				if( driver.workLeft <= 0 )
					driver.ReadyForNextToil();

				//Allow job override periodically
				if( curJob.bill.recipe.UsesUnfinishedThing )
				{
					int billTicks = Find.TickManager.TicksGame - driver.billStartTick;
					if( billTicks >= MinWorkDuration && billTicks%CheckOverrideInterval == 0 )
						actor.jobs.CheckForJobOverride();
				}
			};
		toil.defaultCompleteMode = ToilCompleteMode.Never;
		toil.WithEffect(	()=>toil.actor.CurJob.bill.recipe.effectWorking, TargetIndex.A );
		toil.PlaySustainerOrSound( ()=>toil.actor.CurJob.bill.recipe.soundWorking );
		toil.WithProgressBar(JobDriver_DoBill.BillGiverInd, () =>
			{
				var actor = toil.actor;
				var curJob = actor.CurJob;
				var uft = curJob.GetTarget(JobDriver_DoBill.IngredientInd).Thing as UnfinishedThing;

				return 1f - ((JobDriver_DoBill)actor.jobs.curDriver).workLeft / curJob.bill.recipe.WorkAmountTotal(uft != null ? uft.Stuff : null);
			});
		toil.FailOn( ()=>toil.actor.CurJob.bill.suspended );
		toil.activeSkill = () => toil.actor.CurJob.bill.recipe.workSkill;

		return toil;
	}

	public static Toil FinishRecipeAndStartStoringProduct()
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
			{
				var actor = toil.actor;
				var curJob = actor.jobs.curJob;
				var driver = ((JobDriver_DoBill)actor.jobs.curDriver);

				//Learn (if the recipe doesn't use unfinished thing)
				if( curJob.RecipeDef.workSkill != null && !curJob.RecipeDef.UsesUnfinishedThing )
				{
					float xp = driver.ticksSpentDoingRecipeWork * SkillTuning.XpPerTickRecipeBase * curJob.RecipeDef.workSkillLearnFactor;
					actor.skills.GetSkill(curJob.RecipeDef.workSkill).Learn(xp);
				}

				//Calculate ingredients
				List<Thing> ingredients = CalculateIngredients(curJob, actor);
				Thing dominantIngredient = CalculateDominantIngredient( curJob, ingredients );

				//Make the products
				var products = GenRecipe.MakeRecipeProducts( curJob.RecipeDef, actor, ingredients, dominantIngredient, driver.BillGiver ).ToList();

				//Consume the ingredients
				ConsumeIngredients(ingredients, curJob.RecipeDef, actor.Map);

				//Notify bill
                curJob.bill.Notify_IterationCompleted(actor, ingredients);

				//Add records
				RecordsUtility.Notify_BillDone(actor, products);

				//Add tale
				var uft = curJob.GetTarget(JobDriver_DoBill.IngredientInd).Thing as UnfinishedThing;
				if( curJob.bill.recipe.WorkAmountTotal(uft != null ? uft.Stuff : null) >= LongCraftingProjectThreshold && products.Count > 0 )
					TaleRecorder.RecordTale(TaleDefOf.CompletedLongCraftingProject, actor, products[0].GetInnerIfMinified().def);

				//----------------------------------------------------
				//Rearrange the job so the bill doer goes and stores the product
				//----------------------------------------------------

				//Nothing to store? End the job now
				if( products.Count == 0 )
				{
					actor.jobs.EndCurrentJob( JobCondition.Succeeded );
					return;
				}

				//Bill is set to drop-on-floor mode?
				//Drop everything and end the job now
				if( curJob.bill.GetStoreMode() == BillStoreModeDefOf.DropOnFloor )
				{
					for( int i=0; i<products.Count; i++ )
					{
						if( !GenPlace.TryPlaceThing(products[i], actor.Position, actor.Map, ThingPlaceMode.Near ) )
							Log.Error( actor + " could not drop recipe product " + products[i] + " near " + actor.Position );
					}

					actor.jobs.EndCurrentJob( JobCondition.Succeeded );
					return;
				}
				
				//Place all products except the first one on the ground
				if( products.Count > 1 )
				{
					for( int i=1; i<products.Count; i++ )
					{
						if( !GenPlace.TryPlaceThing(products[i], actor.Position, actor.Map, ThingPlaceMode.Near ) )
							Log.Error( actor + " could not drop recipe product " + products[i] + " near " + actor.Position );
					}
				}

				//Try find a cell to take the product to
				IntVec3 storeCell = IntVec3.Invalid;
				if( curJob.bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile )
					StoreUtility.TryFindBestBetterStoreCellFor( products[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, out storeCell );
				else if( curJob.bill.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile )
					StoreUtility.TryFindBestBetterStoreCellForIn( products[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, curJob.bill.GetStoreZone().slotGroup, out storeCell );
				else
					Log.ErrorOnce("Unknown store mode", 9158246);
				
				if( storeCell.IsValid )
				{
					//Start carrying the first product and proceed to the storage toils
					actor.carryTracker.TryStartCarry( products[0] );

					curJob.targetB = storeCell;
					curJob.targetA = products[0];
					curJob.count = 99999;
				}
				else
				{
					//No store cell? Drop the product; we're done.
					if( !GenPlace.TryPlaceThing( products[0], actor.Position, actor.Map, ThingPlaceMode.Near ) )
					{
						Log.Error("Bill doer could not drop product " + products[0] + " near " + actor.Position );
					}

					actor.jobs.EndCurrentJob( JobCondition.Succeeded );
					return;
				}
			};

		return toil;
	}



	//============================================================================================
	//================================= Products creation helpers ================================
	//============================================================================================

	/// <summary>
	/// Gathers a list of ingredients used by a job, whether they are on the table or inside an UnfinishedThing.
	/// Clears the placedTargets so we don't retain useless references to destroyed things.
	/// </summary>
	private static List<Thing> CalculateIngredients( Job job, Pawn actor )
	{
		//Pull the ingredients from the unfinished thing and destroy it
		UnfinishedThing uft = job.GetTarget( JobDriver_DoBill.IngredientInd ).Thing as UnfinishedThing;
		if( uft != null )
		{
			var ufIngs = uft.ingredients;
			job.RecipeDef.Worker.ConsumeIngredient(uft, job.RecipeDef, actor.Map);	//Todo remove? Really hacky to have this here
			job.placedThings = null;
			return ufIngs;
		}

		//Pull the ingredients from what is currently on the work table
		List<Thing> ingredients = new List<Thing>();
		if( job.placedThings != null )
		{
			for( int i = 0; i < job.placedThings.Count; i++ )
			{
				if( job.placedThings[i].Count <= 0 )
				{
					Log.Error("PlacedThing " + job.placedThings[i] + " with count " + job.placedThings[i].Count + " for job " + job);
					continue;
				}

				//We split off the part of the stack we actually want to consume
				//Note that we avoid splitting for cases where we want the whole stack. This is because we don't
				//want to split off Corpses because then they lose their MapHeld and can't spawn stripped apparel or butcher products.
				Thing ingredient;
				if( job.placedThings[i].Count < job.placedThings[i].thing.stackCount )
					ingredient = job.placedThings[i].thing.SplitOff(job.placedThings[i].Count);
				else
					ingredient = job.placedThings[i].thing;

				job.placedThings[i].Count = 0;

				//Error catch
				//Maybe related to double-destroy below
				if( ingredients.Contains(ingredient) )
				{
					Log.Error("Tried to add ingredient from job placed targets twice: " + ingredient );
					continue;
				}

    			ingredients.Add( ingredient );

				//Auto-strip anything
				//Note: This happens even if the strippable is unspawned, and thus only works
				//		because the strippable should still have a last good position.
				if( job.RecipeDef.autoStripCorpses )
				{
					IStrippable stripIng = ingredient as IStrippable;
					if( stripIng != null )
						stripIng.Strip();
				}
    		}
		}

		job.placedThings = null;

		return ingredients;
	}

	/// <summary>
	/// Gets the dominant ingredient that determines the stuff, color, and possibly other properties of recipe products.
	/// </summary>
	private static Thing CalculateDominantIngredient(Job job, List<Thing> ingredients)
	{
		UnfinishedThing uft = job.GetTarget( JobDriver_DoBill.IngredientInd ).Thing as UnfinishedThing;
		if( uft != null && uft.def.MadeFromStuff )
			return uft.ingredients.First(ing => ing.def == uft.Stuff);

		if( !ingredients.NullOrEmpty() )
		{
			// if the recipe uses stuff ingredient (from recipe maker), then the first ingredient is the dominant one (stuff)
			if( job.RecipeDef.productHasIngredientStuff )
				return ingredients[0];

			if( job.RecipeDef.products.Any(x => x.thingDef.MadeFromStuff) )
				return ingredients.Where(x => x.def.IsStuff).RandomElementByWeight(x => x.stackCount);
			else
				return ingredients.RandomElementByWeight(x => x.stackCount);
		}

		return null;
	}

	private static void ConsumeIngredients( List<Thing> ingredients, RecipeDef recipe, Map map )
	{
		for( int i=0; i<ingredients.Count; i++ )
		{
			recipe.Worker.ConsumeIngredient(ingredients[i], recipe, map );
		}
	}
}
}


#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public class HarvestResource : Activity
	{
		readonly Harvester harv;
		readonly HarvesterInfo harvInfo;
		readonly IFacing facing;
		readonly ResourceClaimLayer claimLayer;
		readonly ResourceLayer resLayer;
		readonly BodyOrientation body;
		readonly IMove move;
		readonly CPos targetCell;

		public HarvestResource(Actor self, CPos targetCell)
		{
			harv = self.Trait<Harvester>();
			harvInfo = self.Info.TraitInfo<HarvesterInfo>();
			facing = self.Trait<IFacing>();
			body = self.Trait<BodyOrientation>();
			move = self.Trait<IMove>();
			claimLayer = self.World.WorldActor.Trait<ResourceClaimLayer>();
			resLayer = self.World.WorldActor.Trait<ResourceLayer>();
			this.targetCell = targetCell;
		}

		protected override void OnFirstRun(Actor self)
		{
			// We can safely assume the claim is successful, since this is only called in the
			// same actor-tick as the targetCell is selected. Therefore no other harvester
			// would have been able to claim.
			claimLayer.TryClaimCell(self, targetCell);
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || harv.IsFull)
				return true;

			// Move towards the target cell
			if (self.Location != targetCell)
			{
				foreach (var n in self.TraitsImplementing<INotifyHarvesterAction>())
					n.MovingToResources(self, targetCell);

				self.SetTargetLine(Target.FromCell(self.World, targetCell), Color.Red, false);
				QueueChild(move.MoveTo(targetCell, 2));
				return false;
			}

			if (!harv.CanHarvestCell(self, self.Location))
				return true;

			// Turn to one of the harvestable facings
			if (harvInfo.HarvestFacings != 0)
			{
				var current = facing.Facing;
				var desired = body.QuantizeFacing(current, harvInfo.HarvestFacings);
				if (desired != current)
				{
					QueueChild(new Turn(self, desired));
					return false;
				}
			}

			var resource = resLayer.Harvest(self.Location);
			if (resource == null)
				return true;

			harv.AcceptResource(self, resource);

			foreach (var t in self.TraitsImplementing<INotifyHarvesterAction>())
				t.Harvested(self, resource);

			QueueChild(new Wait(harvInfo.BaleLoadDelay));
			return false;
		}

		protected override void OnLastRun(Actor self)
		{
			claimLayer.RemoveClaim(self);
		}
	}
}

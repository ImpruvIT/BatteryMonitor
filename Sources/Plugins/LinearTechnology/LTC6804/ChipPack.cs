﻿using System;
using System.Collections.Generic;
using System.Linq;
using ImpruvIT.BatteryMonitor.Domain;
using ImpruvIT.Contracts;

using ImpruvIT.BatteryMonitor.Domain.Battery;

namespace ImpruvIT.BatteryMonitor.Protocols.LinearTechnology.LTC6804
{
	public class ChipPack : SeriesPack
	{
		public ChipPack(int chainIndex, IDictionary<int, SingleCell> cells)
			: base(cells.OrderBy(x => x.Key).Select(x => x.Value))
		{
			this.ChainIndex = chainIndex;
			this.ConnectedCells = cells.ToDictionary(x => x.Key, x=> x.Value);

			this.InitializeReadings();
		}

		public int ChainIndex { get; }
		public IReadOnlyDictionary<int, SingleCell> ConnectedCells { get; }

		public IEnumerable<int> ConnectedChannels
		{
			get { return this.ConnectedCells.Keys.OrderBy(x => x).ToArray(); }
		}

		public bool IsChannelConnected(int channelIndex)
		{
			Contract.Requires(channelIndex, "channelIndex").ToBeInRange(x => 1 <= x && x <= 12);

			return this.ConnectedCells.ContainsKey(channelIndex);
		}

		public SingleCell GetCell(int channelIndex)
		{
			Contract.Requires(channelIndex, "channelIndex").ToBeInRange(x => 1 <= x && x <= 12);

			SingleCell cell;
			if (!this.ConnectedCells.TryGetValue(channelIndex, out cell))
				throw new ArgumentOutOfRangeException("channelIndex", channelIndex, "There is no cell connected to channel " + channelIndex + ".");

			return cell;
		}

		protected void InitializeReadings()
		{
			this.CustomData.CreateValue(new TypedReadingValue<float>(BatteryActualsWrapper.VoltageKey));
			this.CustomData.CreateValue(this.CreateSumReadingValue(LtDataWrapper.SumOfCellVoltagesKey, BatteryActualsWrapper.VoltageKey));
			this.CustomData.CreateValue(new TypedReadingValue<float>(BatteryActualsWrapper.TemperatureKey));
		}
	}
}

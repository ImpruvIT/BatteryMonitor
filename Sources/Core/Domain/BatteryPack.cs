﻿using System;
using System.Collections.Generic;
using System.Linq;

using ImpruvIT.Contracts;

namespace ImpruvIT.BatteryMonitor.Domain
{
	public abstract class BatteryPack : BatteryElement
	{
		private readonly ProductDefinitionWrapper m_productWrapper;

		protected BatteryPack(IEnumerable<BatteryElement> subElements)
		{
			Contract.Requires(subElements, "subElements")
				.NotToBeNull();
			subElements = subElements.ToList();
			Contract.Requires(subElements, "subElements")
				.NotToBeEmpty();

			this.m_productWrapper = new ProductDefinitionWrapper(this.CustomData);
			this.SubElements = subElements;
			this.SubElements.ForEach(x => x.ValueChanged += (s, a) => this.OnValueChanged(a));
		}

		public override IProductDefinition Product
		{
			get { return this.m_productWrapper; }
		}

		public IEnumerable<BatteryElement> SubElements { get; private set; }
	}
}

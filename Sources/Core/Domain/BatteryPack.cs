﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using ImpruvIT.Contracts;

namespace ImpruvIT.BatteryMonitor.Domain
{
	public abstract class BatteryPack : BatteryElement, INotifyPropertyChanged
	{
		private readonly ProductDefinitionWrapper m_productWrapper;

		protected BatteryPack(IEnumerable<BatteryElement> subElements)
		{
			Contract.Requires(subElements, "subElements")
				.IsNotNull();
			subElements = subElements.ToList();
			Contract.Requires(subElements, "subElements")
				.IsNotEmpty();

			this.m_productWrapper = new ProductDefinitionWrapper(this.CustomData);
			this.SubElements = subElements;
			this.SubElements.ForEach(x => x.ValueChanged += (s, a) => this.OnValueChanged(a));
		}

		public override IProductDefinition Product
		{
			get { return this.m_productWrapper; }
		}

		public IEnumerable<BatteryElement> SubElements { get; private set; }

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion
	}
}

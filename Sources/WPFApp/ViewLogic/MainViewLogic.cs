﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ImpruvIT.BatteryMonitor.Hardware;
using ImpruvIT.BatteryMonitor.Hardware.Ftdi;

using I2C = ImpruvIT.BatteryMonitor.Hardware.Ftdi.I2C;
using SPI = ImpruvIT.BatteryMonitor.Hardware.Ftdi.SPI;
using SMBus = ImpruvIT.BatteryMonitor.Protocols.SMBus;
using LTC6804 = ImpruvIT.BatteryMonitor.Protocols.LinearTechnology.LTC6804;

namespace ImpruvIT.BatteryMonitor.WPFApp.ViewLogic
{
	public class MainViewLogic : ViewLogicBase
	{
		#region BusDevices

		private ListBase<IBusDevice> m_busDevices;
		public ListBase<IBusDevice> BusDevices
		{
			get { return this.m_busDevices; }
			set
			{
				if (Object.ReferenceEquals(this.m_busDevices, value))
					return;

				this.SelectedBusDevice = null;

				this.m_busDevices = value;
				this.OnPropertyChanged("BusDevices");

				if (this.m_busDevices.Count >= 1)
					this.SelectedBusDevice = this.m_busDevices[0];
			}
		}

		private IBusDevice m_selectedBusDevice;
		public IBusDevice SelectedBusDevice
		{
			get { return this.m_selectedBusDevice; }
			set
			{
				if (Object.ReferenceEquals(this.m_selectedBusDevice, value))
					return;

				this.Connection = null;

				this.m_selectedBusDevice = value;
				this.OnPropertyChanged("SelectedBusDevice");

				this.ConnectToBus();
			}
		}

		private IBusConnection m_connection;
		public IBusConnection Connection
		{
			get { return this.m_connection; }
			set
			{
				if (Object.ReferenceEquals(this.m_connection, value))
					return;

				this.DisconnectFromBus();

				this.m_connection = value;
				this.OnPropertyChanged("Connection");

				this.DiscoverBatteries();
			}
		}

		public Task DiscoverBusDevices()
		{
			IEnumerable<IDiscoverDevices> dicoveryServices = GetDiscoveryServices();
			var discoveryTask = Task.WhenAll(dicoveryServices.Select(x => x.GetConnectors()));
			return discoveryTask.ContinueWith(t =>
			{
				var devices = new ListBase<IBusDevice>();
				if (!t.IsFaulted && !t.IsCanceled)
					devices.AddRange(t.Result.SelectMany(x => x));

				this.BusDevices = new ListBase<IBusDevice>(devices.OfType<SPI.Device>());
			});
		}

		protected virtual IEnumerable<IDiscoverDevices> GetDiscoveryServices()
		{
			return new IDiscoverDevices[] { new MpsseDiscoveryService() };
		}

		public Task ConnectToBus()
		{
			var busDevice = this.SelectedBusDevice;
			if (busDevice == null)
				return Task.CompletedTask;

			return busDevice.Connect()
				.ContinueWith(t =>
				{
					IBusConnection connection = null;

					if (!t.IsFaulted && !t.IsCanceled)
						connection = t.Result;

					this.Connection = connection;
				});
		}

		public Task DisconnectFromBus()
		{
			var connection = this.Connection;
			if (connection == null)
				return Task.CompletedTask;

			return connection.Disconnect();
		}

		#endregion BusDevices


		#region Batteries

		public ListBase<BatteryLogic> Batteries
		{
			get { return this.m_batteries; }
			set
			{
				if (Object.ReferenceEquals(this.m_batteries, value))
					return;

				this.m_batteries = value;
				this.OnPropertyChanged("Batteries");

				this.FirstBatteryLogic = (this.m_batteries != null ? this.m_batteries.FirstOrDefault() : null);
			}
		}
		private ListBase<BatteryLogic> m_batteries;

		public BatteryLogic FirstBatteryLogic
		{
			get { return this.m_firstBatteryLogic; }
			set
			{
				if (Object.ReferenceEquals(this.m_firstBatteryLogic, value))
					return;

				if (this.m_firstBatteryLogic != null)
					this.m_firstBatteryLogic.StopMonitoring();

				this.m_firstBatteryLogic = value;
				this.OnPropertyChanged("FirstBatteryLogic");

				if (this.m_firstBatteryLogic != null)
					this.m_firstBatteryLogic.StartMonitoring();
			}
		}
		private BatteryLogic m_firstBatteryLogic;

		public void DiscoverBatteries()
		{
			var batteries = new ListBase<BatteryLogic>();

			var connection = this.Connection as ICommunicateToAddressableBus;
			if (connection != null)
				batteries.Add(new BatteryLogic(new SMBus.BatteryAdapter(new SMBus.SMBusInterface(connection), 0x2a)));

			//var connection = this.Connection as ICommunicateToBus;
			//if (connection != null)
			//	batteries.Add(new BatteryLogic(new LTC6804.BatteryAdapter(connection)));

			this.Batteries = batteries;
		}

		#endregion Batteries
	}
}


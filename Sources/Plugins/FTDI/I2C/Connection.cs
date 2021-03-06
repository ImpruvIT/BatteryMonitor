﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FTD2XX_NET;
using ImpruvIT.BatteryMonitor.Hardware.Ftdi.I2C;
using ImpruvIT.Contracts;

namespace ImpruvIT.BatteryMonitor.Hardware.Ftdi.I2C
{
	/// <summary>
	/// A FTDI-based SMBus device connection.
	/// </summary>
	public class Connection : IBusConnection, ICommunicateToAddressableBus
	{
		private const NativeMethods_I2C.TransferOptions WriteOptions =
			NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_BREAK_ON_NACK | 
            NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_FAST_TRANSFER_BYTES;

        private const NativeMethods_I2C.TransferOptions ReadOptions = NativeMethods_I2C.TransferOptions.None;

		#region Connection

		/// <summary>
		/// A connected FTDI device info node.
		/// </summary>
		protected NativeMethods.FT_DEVICE_LIST_INFO_NODE DeviceNode { get; set; }

		protected IntPtr ChannelHandle { get; set; }

		/// <summary>
		/// Gets a value whether connection is connected.
		/// </summary>
		public bool IsConnected { get { return this.ChannelHandle != IntPtr.Zero; } }

		/// <summary>
		/// Connects to the FTDI device.
		/// </summary>
		/// <param name="serialNumber">A FTDI device serial number.</param>
		/// <param name="deviceChannelIndex">A channel index in the device.</param>
		public virtual Task Connect(string serialNumber, int deviceChannelIndex)
		{
			return Task.Run(() =>
			{
				FTDI.FT_STATUS status;

				NativeMethods.Init_libMPSSE();

				// Find requested channel
				uint channelCount;
				status = NativeMethods_I2C.I2C_GetNumChannels(out channelCount);
				if (status != FTDI.FT_STATUS.FT_OK)
					throw new CommunicationException("Unable to find number of I2C channels. (Status: " + status + ")");

				uint channelIndex;
				var deviceNode = new NativeMethods.FT_DEVICE_LIST_INFO_NODE();
				var tmpChannelIndex = deviceChannelIndex;
				for (channelIndex = 0; channelIndex < channelCount; channelIndex++)
				{
					status = NativeMethods_I2C.I2C_GetChannelInfo(0, deviceNode);
					if (status != FTDI.FT_STATUS.FT_OK)
						throw new CommunicationException("Unable to get information about channel " + channelIndex + ". (Status: " + status + ")");

					if (deviceNode.SerialNumber == serialNumber)
					{
						if (tmpChannelIndex > 0)
							tmpChannelIndex--;
						else
							break;
					}
				}

				if (channelIndex >= channelCount)
					throw new CommunicationException("Unable to find channel " + deviceChannelIndex + " on device with serial number '" + serialNumber + "'.");

				this.DeviceNode = deviceNode;

				// Open channel
				IntPtr handle;
				status = NativeMethods_I2C.I2C_OpenChannel(channelIndex, out handle);
				if (status != FTDI.FT_STATUS.FT_OK)
					throw new CommunicationException("Unable to open I2C channel. (Status: " + status + ")");

				this.ChannelHandle = handle;

				// Configure channel
				// NativeMethods.ClockRate.Standard
				var config = new NativeMethods_I2C.ChannelConfig((NativeMethods_I2C.ClockRate)10000, 1, NativeMethods_I2C.ConfigOptions.I2C_ENABLE_DRIVE_ONLY_ZERO);
				status = NativeMethods_I2C.I2C_InitChannel(this.ChannelHandle, config);
				if (status != FTDI.FT_STATUS.FT_OK)
					throw new CommunicationException("Unable to initialize I2C channel. (Status: " + status + ")");
			});
		}

		/// <summary>
		/// Disconnects from device.
		/// </summary>
		public Task Disconnect()
		{
			if (this.ChannelHandle == IntPtr.Zero)
				return Task.CompletedTask;

			// Close the channel
			return Task.Run(() =>
			{
				var status = NativeMethods_I2C.I2C_CloseChannel(this.ChannelHandle);
				this.DeviceNode = null;
				this.ChannelHandle = IntPtr.Zero;
				if (status != FTDI.FT_STATUS.FT_OK)
					throw new CommunicationException("Unable to close I2C channel. (Status: " + status + ")");
			});
		}

		#endregion Connection

		public Task Send(uint address, byte[] data)
		{
			Contract.Requires(data, "data")
				.NotToBeNull();

			return Task.Run(() =>
			{
				uint transferredSize;

				var status = NativeMethods_I2C.I2C_DeviceWrite(
					this.ChannelHandle, 
					address, 
					(uint)data.Length, 
					data, 
					out transferredSize,
                    WriteOptions | 
                        NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_START_BIT | 
                        NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_STOP_BIT);

				if (status != FTDI.FT_STATUS.FT_OK || transferredSize != data.Length)
					throw new CommunicationException("Error while writing to the bus. (Status: " + status + ")");
			});
		}

		public Task<byte[]> Receive(uint address, int dataLength)
		{
			Contract.Requires(dataLength, "dataLength")
				.ToBeInRange(x => x > 0);

			return Task.Run(() =>
			{
				var buffer = new byte[dataLength];
				uint transferredSize;

				var status = NativeMethods_I2C.I2C_DeviceRead(
					this.ChannelHandle,
					address,
					(uint)dataLength,
					buffer,
					out transferredSize,
                    ReadOptions |
                        NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_START_BIT |
                        NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_STOP_BIT);

				if (status != FTDI.FT_STATUS.FT_OK)
					throw new CommunicationException("Error while reading from the bus. (Status: " + status + ")");

				if (transferredSize < dataLength)
				{
					// If less bytes receive as expected => Copy to smaller array 
					var tmpData = new byte[transferredSize];
					Array.Copy(buffer, tmpData, transferredSize);
					buffer = tmpData;
				}

				return buffer;
			});
		}

		public Task<byte[]> Transceive(uint address, byte[] dataToSend, int receiveLength)
		{
			Contract.Requires(dataToSend, "dataToSend").NotToBeNull();
			Contract.Requires(receiveLength, "receiveLength").ToBeInRange(x => x > 0);

			return Task.Run(() =>
			{
				uint transferredSize;

				// Send data
				var status = NativeMethods_I2C.I2C_DeviceWrite(
					this.ChannelHandle,
					address,
					(uint)dataToSend.Length,
					dataToSend,
					out transferredSize,
					WriteOptions | NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_START_BIT);

				if (status != FTDI.FT_STATUS.FT_OK || transferredSize != dataToSend.Length)
					throw new CommunicationException("Error while writing to the bus. (Status: " + status + ")");

				// Receive data
				var buffer = new byte[receiveLength];

				status = NativeMethods_I2C.I2C_DeviceRead(
					this.ChannelHandle,
					address,
					(uint)receiveLength,
					buffer,
					out transferredSize,
                    ReadOptions |
                        NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_START_BIT |
                        NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_STOP_BIT |
                        NativeMethods_I2C.TransferOptions.I2C_TRANSFER_OPTIONS_NACK_LAST_BYTE);

				if (status != FTDI.FT_STATUS.FT_OK)
					throw new CommunicationException("Error while reading from the bus. (Status: " + status + ")");

				if (transferredSize < receiveLength)
				{
					// If less bytes receive as expected => Copy to smaller array 
					var tmpData = new byte[transferredSize];
					Array.Copy(buffer, tmpData, transferredSize);
					buffer = tmpData;
				}

				return buffer;
			});
		}
	}
}

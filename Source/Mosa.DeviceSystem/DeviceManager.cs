﻿// Copyright (c) MOSA Project. Licensed under the New BSD License.

using System.Collections.Generic;

namespace Mosa.DeviceSystem
{
	/// <summary>
	/// Device Manager
	/// </summary>
	public sealed class DeviceManager
	{
		/// <summary>
		/// The maximum interrupts
		/// </summary>
		public const ushort MaxInterrupts = 32;

		/// <summary>
		/// Gets the platform architecture.
		/// </summary>
		public PlatformArchitecture PlatformArchitecture { get; }

		/// <summary>
		/// The registered device drivers
		/// </summary>
		private readonly List<DeviceDriverRegistryEntry> registry;

		/// <summary>
		/// The devices
		/// </summary>
		private readonly List<Device> devices;

		/// <summary>
		/// The spin lock
		/// </summary>
		private SpinLock spinLock;

		/// <summary>
		/// The spin lock
		/// </summary>
		private SpinLock onChangeSpinLock;

		/// <summary>
		/// The interrupt handlers
		/// </summary>
		private readonly List<Device>[] irqDispatch;

		/// <summary>
		/// The mount daemons
		/// </summary>
		private readonly List<BaseMountDaemon> daemons;

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceManager" /> class.
		/// </summary>
		/// <param name="platform">The platform.</param>
		public DeviceManager(PlatformArchitecture platform)
		{
			registry = new List<DeviceDriverRegistryEntry>();
			devices = new List<Device>();
			daemons = new List<BaseMountDaemon>();

			irqDispatch = new List<Device>[MaxInterrupts];
			PlatformArchitecture = platform;

			for (int i = 0; i < MaxInterrupts; i++)
			{
				irqDispatch[i] = new List<Device>();
			}
		}

		#region Device Driver Registry

		public void RegisterDeviceDriver(DeviceDriverRegistryEntry deviceDriver)
		{
			try
			{
				spinLock.Enter();

				registry.Add(deviceDriver);
			}
			finally
			{
				spinLock.Exit();
			}
		}

		public List<DeviceDriverRegistryEntry> GetDeviceDrivers(DeviceBusType busType)
		{
			var drivers = new List<DeviceDriverRegistryEntry>();

			try
			{
				spinLock.Enter();

				foreach (var deviceDriver in registry)
				{
					if (deviceDriver.BusType == busType)
					{
						drivers.Add(deviceDriver);
					}
				}
			}
			finally
			{
				spinLock.Exit();
			}

			return drivers;
		}

		public bool CheckExists(Device parent, ulong componentID)
		{
			try
			{
				spinLock.Enter();

				foreach (var device in devices)
				{
					if (device.Parent == parent && device.ComponentID == componentID)
					{
						return true;
					}
				}
			}
			finally
			{
				spinLock.Exit();
			}

			return false;
		}

		#endregion Device Driver Registry

		#region Initialize Devices Drivers

		public Device Initialize(DeviceDriverRegistryEntry deviceDriverRegistryEntry, Device parent, BaseDeviceConfiguration configuration = null, HardwareResources resources = null)
		{
			var deviceDriver = deviceDriverRegistryEntry.Factory();

			return Initialize(deviceDriver, parent, configuration, resources, deviceDriverRegistryEntry);
		}

		public Device Initialize(BaseDeviceDriver deviceDriver, Device parent, BaseDeviceConfiguration configuration = null, HardwareResources resources = null, DeviceDriverRegistryEntry deviceDriverRegistryEntry = null)
		{
			var device = new Device()
			{
				DeviceDriver = deviceDriver,
				DeviceDriverRegistryEntry = deviceDriverRegistryEntry,
				Status = DeviceStatus.Initializing,
				Parent = parent,
				Configuration = configuration,
				Resources = resources,
				DeviceManager = this,
			};

			Initialize(device);

			OnChange(device);

			return device;
		}

		/// <summary>
		/// Adds the specified device.
		/// </summary>
		/// <param name="device">The device.</param>
		private void Initialize(Device device)
		{
			spinLock.Enter();
			devices.Add(device);

			if (device.Parent != null)
			{
				device.Parent.Children.Add(device);
			}

			spinLock.Exit();

			Start(device);
		}

		private void Start(Device device)
		{
			device.Status = DeviceStatus.Initializing;

			device.DeviceDriver.Setup(device);

			device.DeviceDriver.Initialize();

			if (device.Status == DeviceStatus.Initializing)
			{
				device.DeviceDriver.Probe();

				if (device.Status == DeviceStatus.Available)
				{
					device.DeviceDriver.Start();
				}
			}
		}

		#endregion Initialize Devices Drivers

		#region Get Devices

		public List<Device> GetDevices<T>()
		{
			var list = new List<Device>();

			try
			{
				spinLock.Enter();

				foreach (var device in devices)
				{
					if (device.DeviceDriver is T)
					{
						list.Add(device);
					}
				}
			}
			finally
			{
				spinLock.Exit();
			}

			return list;
		}

		public List<Device> GetDevices<T>(DeviceStatus status)
		{
			var list = new List<Device>();

			try
			{
				spinLock.Enter();

				foreach (var device in devices)
				{
					if (device.Status == status && device.DeviceDriver is T)
					{
						list.Add(device);
					}
				}
			}
			finally
			{
				spinLock.Exit();
			}

			return list;
		}

		public List<Device> GetDevices(string name)
		{
			var list = new List<Device>();

			try
			{
				spinLock.Enter();

				foreach (var device in devices)
				{
					if (device.Name == name)
					{
						list.Add(device);
					}
				}
			}
			finally
			{
				spinLock.Exit();
			}

			return list;
		}

		public List<Device> GetChildrenOf(Device parent)
		{
			var list = new List<Device>();

			try
			{
				spinLock.Enter();

				foreach (var device in parent.Children)
				{
					list.Add(device);
				}
			}
			finally
			{
				spinLock.Exit();
			}

			return list;
		}

		public List<Device> GetAllDevices()
		{
			var list = new List<Device>();

			try
			{
				spinLock.Enter();

				foreach (var device in devices)
				{
					list.Add(device);
				}
			}
			finally
			{
				spinLock.Exit();
			}

			return list;
		}

		#endregion Get Devices

		#region Interrupts

		public void ProcessInterrupt(byte irq)
		{
			try
			{
				spinLock.Enter();

				foreach (var device in irqDispatch[irq])
				{
					var deviceDriver = device.DeviceDriver;
					deviceDriver.OnInterrupt();
				}
			}
			finally
			{
				spinLock.Exit();
			}
		}

		public void AddInterruptHandler(byte irq, Device device)
		{
			if (irq >= MaxInterrupts)
				return;

			try
			{
				spinLock.Enter();
				irqDispatch[irq].Add(device);
			}
			finally
			{
				spinLock.Exit();
			}
		}

		public void ReleaseInterruptHandler(byte irq, Device device)
		{
			if (irq >= MaxInterrupts)
				return;

			try
			{
				spinLock.Enter();
				irqDispatch[irq].Remove(device);
			}
			finally
			{
				spinLock.Exit();
			}
		}

		#endregion Interrupts

		#region Daemons

		public void RegisterDaemon(BaseMountDaemon daemon)
		{
			try
			{
				onChangeSpinLock.Enter();

				daemons.Add(daemon);
			}
			finally
			{
				onChangeSpinLock.Exit();
			}
		}

		public void OnChange(Device device)
		{
			try
			{
				onChangeSpinLock.Enter();

				foreach (var daemon in daemons)
				{
					daemon.OnChange(device);
				}
			}
			finally
			{
				onChangeSpinLock.Exit();
			}
		}

		#endregion Daemons
	}
}

using System;
using OpenBveApi.Runtime;

namespace Plugin {
	/// <summary>Represents the TASC device.</summary>
	internal class Tasc : Device {
		
		// --- enumerations ---
		
		internal enum States {
			/// <summary>The system is operating normally.</summary>
			Normal = 1,
			/// <summary>The system has received a pattern.</summary>
			Pattern = 2,
			/// <summary>The system is released after having stopped at a previous stop point.</summary>
			Released = 3
		}
		
		
		// --- members ---
		
		/// <summary>The underlying train.</summary>
		private Train Train;
		
		/// <summary>The current state of the system.</summary>
		internal States State;
		
		/// <summary>Whether to override the system and prevent it from braking the train at stations.</summary>
		internal bool Override;
		
		/// <summary>The distance to the next stop point.</summary>
		private double Distance;
		
		/// <summary>Whether the train approaches the stop point.</summary>
		private bool Approach;
		
		/// <summary>The currently selected brake notch.</summary>
		private int BrakeNotch;

		/// <summary>The timer that counts until the brake control delay.</summary>
		private double BrakeControlTimer;

		/// <summary>Whether the station has home doors.</summary>
		private bool HomeDoors;
		
		
		// --- parameters --
		
		/// <summary>The deceleration for regular TASC brake operation.</summary>
		internal double TascBrakeDeceleration = 2.57 / 3.6;
		
		/// <summary>The stopping tolerance in meters.</summary>
		internal double TascBrakeTolerance = 0.15;
		
		/// <summary>The maximum service brake deceleration.</summary>
		internal double ServiceBrakeDeceleration = 4.5 / 3.6;

		/// <summary>The speed at which the brake control system applies.</summary>
		internal double BrakeControlSpeed = 15.0 / 3.6;
		
		/// <summary>The delay until full service brake application.</summary>
		internal double BrakeControlDelay = 2.0;
		
		/// <summary>The route-specific tolerance for home doors.</summary>
		private const double HomeDoorsTolerance = 0.35;

		
		// --- constructors ---
		
		/// <summary>Creates a new instance of this system.</summary>
		/// <param name="train">The train.</param>
		internal Tasc(Train train) {
			this.Train = train;
			this.State = States.Released;
			this.Distance = 0.0;
			this.Approach = false;
			this.BrakeNotch = 0;
			this.BrakeControlTimer = 0.0;
			this.HomeDoors = false;
		}
		
		
		// --- inherited functions ---
		
		/// <summary>Is called when the system should initialize.</summary>
		/// <param name="mode">The initialization mode.</param>
		internal override void Initialize(InitializationModes mode) {
			this.State = States.Released;
			this.Distance = 0.0;
			this.Approach = false;
			this.BrakeNotch = 0;
			this.BrakeControlTimer = 0.0;
			this.HomeDoors = false;
		}

		/// <summary>Is called every frame.</summary>
		/// <param name="data">The data.</param>
		/// <param name="blocking">Whether the device is blocked or will block subsequent devices.</param>
		internal override void Elapse(ElapseData data, ref bool blocking) {
			// --- behavior ---
			if (this.State == States.Pattern | this.State == States.Released) {
				this.Distance -= data.Vehicle.Speed.MetersPerSecond * data.ElapsedTime.Seconds;
			}
			if (this.State == States.Pattern) {
				if (this.Override) {
					// --- override ---
					this.BrakeNotch = 0;
					if (this.Distance < -this.TascBrakeTolerance) {
						this.State = States.Released;
					}
					data.DebugMessage = "TASC override";
				} else if (this.Train.Doors == DoorStates.None) {
					// --- doors closed ---
					int notchOffset = this.Train.Specs.HasHoldBrake ? 1 : 0;
					double distance = this.Distance;
					bool hold = false;
					if (data.Vehicle.Speed.MetersPerSecond >= this.BrakeControlSpeed) {
						distance -= this.BrakeControlSpeed * this.BrakeControlDelay;
						this.BrakeControlTimer = 0.0;
					} else {
						double delay = this.BrakeControlDelay * (double)(this.BrakeNotch - notchOffset) / (double)(this.Train.Specs.BrakeNotches - notchOffset);
						if (this.BrakeControlTimer < delay) {
							this.BrakeControlTimer += data.ElapsedTime.Seconds;
							hold = true;
						} else {
							this.BrakeControlTimer = this.BrakeControlDelay;
						}
					}
					if (!hold) {
						bool adjust;
						if (this.BrakeNotch == 0) {
							adjust = true;
						} else {
							double deceleration = this.ServiceBrakeDeceleration * (double)(this.BrakeNotch - notchOffset) / (double)(this.Train.Specs.BrakeNotches - notchOffset);
							double deviation = distance - data.Vehicle.Speed.MetersPerSecond * data.Vehicle.Speed.MetersPerSecond / (2.0 * deceleration);
							adjust = Math.Abs(deviation) > this.TascBrakeTolerance;
						}
						if (adjust) {
							if (distance > 0.0) {
								double requiredDeceleration = data.Vehicle.Speed.MetersPerSecond * data.Vehicle.Speed.MetersPerSecond / (2.0 * distance);
								double requiredBrakeNotch = notchOffset + requiredDeceleration / this.ServiceBrakeDeceleration * (this.Train.Specs.BrakeNotches - notchOffset);
								if (!this.Approach) {
									const double amplifier = 5.0;
									requiredDeceleration = (requiredDeceleration - this.TascBrakeDeceleration) * amplifier + this.TascBrakeDeceleration;
									double notch = notchOffset + requiredDeceleration / this.ServiceBrakeDeceleration * (this.Train.Specs.BrakeNotches - notchOffset);
									if ((int)Math.Round(notch) < (int)Math.Round(requiredBrakeNotch)) {
										requiredBrakeNotch = notch;
									} else {
										this.Approach = true;
									}
								}
								if (requiredBrakeNotch < this.BrakeNotch) {
									this.BrakeNotch = (int)Math.Ceiling(requiredBrakeNotch - 0.25);
								} else if (requiredBrakeNotch > this.BrakeNotch) {
									this.BrakeNotch = (int)Math.Ceiling(requiredBrakeNotch - 0.75);
								}
								if (this.BrakeNotch <= notchOffset) {
									this.BrakeNotch = 0;
								} else if (this.BrakeNotch > this.Train.Specs.BrakeNotches) {
									this.BrakeNotch = this.Train.Specs.BrakeNotches;
								}
							} else {
								this.BrakeNotch = this.Train.Specs.BrakeNotches;
							}
						}
					}
					if (data.Handles.BrakeNotch < this.BrakeNotch) {
						data.Handles.BrakeNotch = this.BrakeNotch;
					}
					data.DebugMessage = "TASC pattern @" + this.Distance.ToString("0.00");
				} else {
					// --- doors opened ---
					if (data.Handles.BrakeNotch < this.Train.Specs.BrakeNotches) {
						data.Handles.BrakeNotch = this.Train.Specs.BrakeNotches;
					}
					data.DebugMessage = "TASC doorblock";
				}
			} else if (this.State == States.Released) {
				this.BrakeNotch = 0;
				if (Math.Abs(this.Distance) > 5.0) {
					this.State = States.Normal;
					this.Distance = 0.0;
					this.Approach = false;
					this.HomeDoors = false;
				}
				data.DebugMessage = "TASC released";
			}
			// --- panel ---
			if (this.Override) {
				this.Train.Panel[83] = 1;
			}
			this.Train.Panel[56] = 0;
			this.Train.Panel[80] = 1;
			if (this.State == States.Pattern) {
				this.Train.Panel[81] = 1;
			}
			if (this.BrakeNotch > 0) {
				this.Train.Panel[82] = 1;
			}
			if ((this.State == States.Pattern) & this.Distance >= -HomeDoorsTolerance & this.Distance <= HomeDoorsTolerance) {
				this.Train.Panel[85] = 1;
			}
			if (this.Train.Doors == DoorStates.None) {
				this.Train.Panel[86] = 1;
			}
			if (!this.HomeDoors | this.Distance < -HomeDoorsTolerance | this.Distance > HomeDoorsTolerance | this.Train.Doors == DoorStates.None) {
				this.Train.Panel[87] = 1;
			}
			this.Train.Panel[90] = this.BrakeNotch;
		}
		
		/// <summary>Is called when a key is pressed.</summary>
		/// <param name="key">The key.</param>
		internal override void KeyDown(VirtualKeys key) {
			switch (key) {
				case VirtualKeys.I:
					this.Override = !this.Override;
					break;
			}
		}
		
		/// <summary>Is called when the state of the doors changes.</summary>
		/// <param name="oldState">The old state of the doors.</param>
		/// <param name="newState">The new state of the doors.</param>
		internal override void DoorChange(DoorStates oldState, DoorStates newState) {
			if (oldState != DoorStates.None & newState == DoorStates.None) {
				if (this.State == States.Pattern) {
					this.State = States.Released;
				}
			}
		}
		
		/// <summary>Is called when a beacon is passed.</summary>
		/// <param name="beacon">The beacon data.</param>
		internal override void SetBeacon(BeaconData beacon) {
			switch (beacon.Type) {
				case 30:
					// --- TASC pattern ---
					int distance = beacon.Optional / 1000;
					this.Distance = (double)distance;
					if (this.State == States.Normal) {
						this.State = States.Pattern;
					}
					break;
				case 31:
					// --- TASC home doors ---
					if (beacon.Signal.Distance <= 0.0 & beacon.Optional == 0) {
						this.HomeDoors = true;
					}
					break;
				case 32:
					// --- TASC pattern ---
					int minimumCars = beacon.Optional / 1000000;
					int maximumCars = (beacon.Optional / 10000) % 100;
					if (this.Train.Specs.Cars >= minimumCars & this.Train.Specs.Cars <= maximumCars | minimumCars == 0 & maximumCars == 0) {
						this.Distance = beacon.Optional % 10000;
						if (this.State == States.Normal) {
							this.State = States.Pattern;
						}
					}
					break;
			}
		}
		
	}
}
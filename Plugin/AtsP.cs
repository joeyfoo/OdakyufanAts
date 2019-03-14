using System;
using System.Collections.Generic;
using System.Text;

using OpenBveApi.Runtime;

namespace Plugin {
	/// <summary>Represents ATS-P.</summary>
	internal class AtsP : Device {
		
		
		// --- enumerations ---
		
		/// <summary>Represents different states of ATS-P.</summary>
		internal enum States {
			/// <summary>The system is disabled.</summary>
			Disabled = 0,
			/// <summary>The system is enabled, but currently suppressed. This will change to States.Initializing once the emergency brakes are released.</summary>
			Suppressed = 1,
			/// <summary>The system is initializing. This will change to States.Standby once the initialization is complete.</summary>
			Initializing = 2,
			/// <summary>The system is available but no ATS-P signal has yet been picked up.</summary>
			Standby = 3,
			/// <summary>The system is operating normally.</summary>
			Normal = 4,
			/// <summary>The system is approaching a brake pattern.</summary>
			Pattern = 5,
			/// <summary>The system is braking due to speed excess.</summary>
			Brake = 6,
			/// <summary>The system applies the service brakes due to an immediate stop command.</summary>
			Service = 7,
			/// <summary>The system applies the emergency brakes due to an immediate stop command.</summary>
			Emergency = 8
		}

		
		// --- pattern ---
		
		/// <summary>Represents a pattern.</summary>
		internal class Pattern {
			// --- members ---
			/// <summary>The underlying ATS-P device.</summary>
			internal AtsP Device;
			/// <summary>The position of the point of danger, or System.Double.MinValue, or System.Double.MaxValue.</summary>
			internal double Position;
			/// <summary>The warning pattern, or System.Double.MaxValue.</summary>
			internal double WarningPattern;
			/// <summary>The brake pattern, or System.Double.MaxValue.</summary>
			internal double BrakePattern;
			/// <summary>The speed limit at the point of danger, or System.Double.MaxValue.</summary>
			internal double TargetSpeed;
			/// <summary>Whether the pattern is persistent, i.e. cannot be cleared.</summary>
			internal bool Persistent;
			// --- constructors ---
			/// <summary>Creates a new pattern.</summary>
			/// <param name="device">A reference to the underlying ATS-P device.</param>
			internal Pattern(AtsP device) {
				this.Device = device;
				this.Position = double.MaxValue;
				this.WarningPattern = double.MaxValue;
				this.BrakePattern = double.MaxValue;
				this.TargetSpeed = double.MaxValue;
				this.Persistent = false;
			}
			// --- functions ---
			/// <summary>Updates the pattern.</summary>
			/// <param name="system">The current ATS-P system.</param>
			internal void Perform(AtsP system) {
				if (this.Position == double.MaxValue | this.TargetSpeed == double.MaxValue) {
					this.WarningPattern = double.MaxValue;
					this.BrakePattern = double.MaxValue;
				} else if (this.Position == double.MinValue) {
					if (this.TargetSpeed > 1.0 / 3.6) {
						this.WarningPattern = this.TargetSpeed + this.Device.WarningPatternTolerance;
						this.BrakePattern = this.TargetSpeed + this.Device.BrakePatternTolerance;
					} else {
						this.WarningPattern = this.TargetSpeed;
						this.BrakePattern = this.TargetSpeed;
					}
					if (this.BrakePattern < this.Device.ReleaseSpeed) {
						this.BrakePattern = this.Device.ReleaseSpeed;
					}
				} else {
					double distance = this.Position - system.Position;
					// --- calculate the warning pattern ---
					{
						double sqrtTerm = 2.0 * this.Device.DesignDeceleration * (distance - this.Device.WarningPatternOffset) + this.Device.DesignDeceleration * this.Device.DesignDeceleration * this.Device.WarningPatternDelay * this.Device.WarningPatternDelay + this.TargetSpeed * this.TargetSpeed;
						this.WarningPattern = (sqrtTerm > 0.0 ? Math.Sqrt(sqrtTerm) : 0.0) - this.Device.DesignDeceleration * this.Device.WarningPatternDelay;
						if (this.TargetSpeed > 1.0 / 3.6) {
							if (this.WarningPattern < this.TargetSpeed + this.Device.WarningPatternTolerance) {
								this.WarningPattern = this.TargetSpeed + this.Device.WarningPatternTolerance;
							}
						} else {
							if (this.WarningPattern < this.TargetSpeed) {
								this.WarningPattern = this.TargetSpeed;
							}
						}
					}
					// --- calculate the brake pattern ---
					{
						double sqrtTerm = 2.0 * this.Device.DesignDeceleration * (distance - this.Device.BrakePatternOffset) + this.Device.DesignDeceleration * this.Device.DesignDeceleration * this.Device.BrakePatternDelay * this.Device.BrakePatternDelay + this.TargetSpeed * this.TargetSpeed;
						this.BrakePattern = (sqrtTerm > 0.0 ? Math.Sqrt(sqrtTerm) : 0.0) - this.Device.DesignDeceleration * this.Device.BrakePatternDelay;
						if (this.TargetSpeed > 1.0 / 3.6) {
							if (this.BrakePattern < this.TargetSpeed + this.Device.BrakePatternTolerance) {
								this.BrakePattern = this.TargetSpeed + this.Device.BrakePatternTolerance;
							}
						} else {
							if (this.BrakePattern < this.TargetSpeed) {
								this.BrakePattern = this.TargetSpeed;
							}
						}
						if (this.BrakePattern < this.Device.ReleaseSpeed) {
							this.BrakePattern = this.Device.ReleaseSpeed;
						}
					}
					
				}
			}
			/// <summary>Sets the position of the red signal.</summary>
			/// <param name="distance">The position.</param>
			internal void SetRedSignal(double position) {
				this.Position = position;
				this.TargetSpeed = 0.0;
			}
			/// <summary>Sets the position of the green signal.</summary>
			/// <param name="distance">The position.</param>
			internal void SetGreenSignal(double position) {
				this.Position = position;
				this.TargetSpeed = double.MaxValue;
			}
			/// <summary>Sets a speed limit and the position of the speed limit.</summary>
			/// <param name="speed">The speed.</param>
			/// <param name="distance">The position.</param>
			internal void SetLimit(double speed, double position) {
				this.Position = position;
				this.TargetSpeed = speed;
			}
			/// <summary>Sets the train-specific permanent speed limit.</summary>
			/// <param name="speed">The speed limit.</param>
			internal void SetPersistentLimit(double speed) {
				this.Position = double.MinValue;
				this.TargetSpeed = speed;
				this.Persistent = true;
			}
			/// <summary>Clears the pattern.</summary>
			internal void Clear() {
				if (!this.Persistent) {
					this.Position = double.MaxValue;
					this.WarningPattern = double.MaxValue;
					this.BrakePattern = double.MaxValue;
					this.TargetSpeed = double.MaxValue;
				}
			}
			/// <summary>Adds a textual representation to the specified string builder if this pattern is not clear.</summary>
			/// <param name="prefix">The textual prefix.</param>
			/// <param name="builder">The string builder.</param>
			internal void AddToStringBuilder(string prefix, StringBuilder builder) {
				if (this.Position >= double.MaxValue | this.TargetSpeed >= double.MaxValue) {
					/* do nothing */
				} else if (this.Position <= double.MinValue) {
					string text = prefix + (3.6 * this.BrakePattern).ToString("0");
					if (builder.Length != 0) {
						builder.Append(", ");
					}
					builder.Append(text);
				} else {
					string text;
					double distance = this.Position - this.Device.Position;
					if (distance <= 0.0) {
						text = prefix + (3.6 * this.BrakePattern).ToString("0");
					} else {
						text = prefix + (3.6 * this.TargetSpeed).ToString("0") + "(" + (3.6 * this.BrakePattern).ToString("0") + ")@" + distance.ToString("0");
					}
					if (builder.Length != 0) {
						builder.Append(", ");
					}
					builder.Append(text);
				}
			}
		}
		
		
		private class CompatibilityLimit : IComparable<CompatibilityLimit>, IEquatable<CompatibilityLimit> {
			// --- members ---
			internal double Limit;
			internal double Location;
			// --- constructors ---
			internal CompatibilityLimit(double limit, double location) {
				this.Limit = limit;
				this.Location = location;
			}
			// --- functions ---
			public int CompareTo(CompatibilityLimit other) {
				int value;
				value = this.Location.CompareTo(other.Location);
				if (value != 0) return value;
				value = this.Limit.CompareTo(other.Limit);
				if (value != 0) return value;
				return 0;
			}
			public bool Equals(CompatibilityLimit other) {
				if (this.Limit != other.Limit) return false;
				if (this.Location != other.Location) return false;
				return true;
			}
			// --- static functions ---
			public static bool IsSuperfluous(AtsP device, CompatibilityLimit current, CompatibilityLimit next) {
				double distance = next.Location - current.Location;
				double targetSpeed = next.Limit;
				double sqrtTerm = 2.0 * device.DesignDeceleration * (distance - device.BrakePatternOffset) + device.DesignDeceleration * device.DesignDeceleration * device.BrakePatternDelay * device.BrakePatternDelay + targetSpeed * targetSpeed;
				double brakePattern = (sqrtTerm > 0.0 ? Math.Sqrt(sqrtTerm) : 0.0) - device.DesignDeceleration * device.BrakePatternDelay;
				return brakePattern <= current.Limit;
			}
			public static void Sort(AtsP device, List<CompatibilityLimit> compatibilityLimits) {
				compatibilityLimits.Sort();
				for (int i = 0; i < compatibilityLimits.Count - 1; i++) {
					if (IsSuperfluous(device, compatibilityLimits[i], compatibilityLimits[i + 1])) {
						compatibilityLimits.RemoveAt(i);
						i--;
					}
				}
			}
		}

//		private class Signal {
//			// --- members ---
//			internal double Location;
//			internal int Aspect;
//			// --- constructors ---
//			internal Signal(double location, int aspect) {
//				this.Location = location;
//				this.Aspect = aspect;
//			}
//		}
		
		
		// --- members ---
		
		/// <summary>The underlying train.</summary>
		private Train Train;
		
		/// <summary>The current state of the system.</summary>
		internal States State;
		
		/// <summary>Whether the system is currently blocked.</summary>
		private bool Blocked;
		
		/// <summary>Whether simultaneous ATS-Sx/P mode is currently active.</summary>
		private bool AtsSxPMode;
		
		/// <summary>Whether the brake release is currently active.</summary>
		private bool BrakeRelease;
		
		/// <summary>The remaining time before the brake release is over.</summary>
		private double BrakeReleaseCountdown;
		
		/// <summary>The current initialization countdown.</summary>
		private double InitializationCountdown;
		
		/// <summary>The position of the train as obtained from odometry.</summary>
		private double Position;
		
		/// <summary>The position at which to switch to ATS-Sx, or System.Double.MaxValue.</summary>
		private double SwitchToAtsSxPosition;
		
		/// <summary>A sorted list of all compatibility temporary speed limits in the route. The key is the position, the value the speed limit.</summary>
		private List<CompatibilityLimit> CompatibilityLimits;

		/// <summary>Whether to sort the compatibility limits by track position in the next Elapse call. Should be set whenever a new compatibility limit is received.</summary>
		private bool CompatibilityLimitsNeedsSort;
		
		/// <summary>The element in the CompatibilityLimits list that holds the next speed limit.</summary>
		private int CompatibilityLimitPointer;
		
		
		// --- patterns ---
		
		/// <summary>The signal patterns.</summary>
		private List<Pattern> SignalPatterns;
		
		/// <summary>The divergence pattern.</summary>
		private Pattern DivergencePattern;
		
		/// <summary>The downslope pattern.</summary>
		private Pattern DownslopePattern;

		/// <summary>The curve pattern.</summary>
		private Pattern CurvePattern;

		/// <summary>The temporary pattern.</summary>
		private Pattern TemporaryPattern;

		/// <summary>The route-specific permanent pattern.</summary>
		private Pattern RoutePermanentPattern;
		
		/// <summary>The train-specific permanent pattern.</summary>
		internal Pattern TrainPermanentPattern;

		/// <summary>The compatibility temporary pattern.</summary>
		private Pattern CompatibilityTemporaryPattern;

		/// <summary>The compatibility permanent pattern.</summary>
		private Pattern CompatibilityPermanentPattern;

		/// <summary>A list of all patterns.</summary>
		private List<Pattern> Patterns;
		
		
		// --- parameters ---

		/// <summary>The duration of the initialization process.</summary>
		internal double DurationOfInitialization = 3.0;

		/// <summary>The duration of the brake release. If zero, brake release is not available.</summary>
		internal double DurationOfBrakeRelease = 60.0;
		
		/// <summary>The design deceleration.</summary>
		internal double DesignDeceleration = 2.445 / 3.6;

		/// <summary>The reaction delay for the brake pattern.</summary>
		internal double BrakePatternDelay = 0.5;

		/// <summary>The signal offset for the brake pattern.</summary>
		internal double BrakePatternOffset = 0.0;
		
		/// <summary>The speed tolerance for the brake pattern.</summary>
		internal double BrakePatternTolerance = 0.0 / 3.6;

		/// <summary>The reaction delay for the warning pattern.</summary>
		internal double WarningPatternDelay = 5.5;

		/// <summary>The signal offset for the warning pattern.</summary>
		internal double WarningPatternOffset = 50.0;

		/// <summary>The speed tolerance for the warning pattern.</summary>
		internal double WarningPatternTolerance = -5.0 / 3.6;

		/// <summary>The release speed.</summary>
		internal double ReleaseSpeed = 15.0 / 3.6;
		
		
		// --- constructors ---
		
		/// <summary>Creates a new instance of this system.</summary>
		/// <param name="train">The train.</param>
		internal AtsP(Train train) {
			this.Train = train;
			this.State = States.Disabled;
			this.AtsSxPMode = false;
			this.InitializationCountdown = 0.0;
			this.SwitchToAtsSxPosition = double.MaxValue;
			this.CompatibilityLimits = new List<CompatibilityLimit>();
			this.CompatibilityLimitsNeedsSort = false;
			this.CompatibilityLimitPointer = 0;
			this.SignalPatterns = new List<Pattern>();
			this.DivergencePattern = new Pattern(this);
			this.DownslopePattern = new Pattern(this);
			this.CurvePattern = new Pattern(this);
			this.TemporaryPattern = new Pattern(this);
			this.RoutePermanentPattern = new Pattern(this);
			this.TrainPermanentPattern = new Pattern(this);
			this.CompatibilityTemporaryPattern = new Pattern(this);
			this.CompatibilityPermanentPattern = new Pattern(this);
			var patterns = new List<Pattern>();
			patterns.Add(this.DivergencePattern);
			patterns.Add(this.DownslopePattern);
			patterns.Add(this.CurvePattern);
			patterns.Add(this.TemporaryPattern);
			patterns.Add(this.RoutePermanentPattern);
			patterns.Add(this.TrainPermanentPattern);
			patterns.Add(this.CompatibilityTemporaryPattern);
			patterns.Add(this.CompatibilityPermanentPattern);
			this.Patterns = patterns;
		}
		
		
		// --- functions ---
		
		/// <summary>Changes to standby mode and continues in ATS-Sx mode.</summary>
		private void SwitchToSx() {
			if (this.Train.AtsSx != null) {
				foreach (var pattern in this.Patterns) {
					pattern.Clear();
				}
				this.State = States.Standby;
				if (!this.Blocked) {
					this.Train.Sounds.AtsPBell.Play();
				}
				this.Train.AtsSx.State = AtsSx.States.Chime;
			} else if (this.State != States.Emergency) {
				this.State = States.Emergency;
				if (this.State != States.Brake & this.State != States.Service) {
					if (!this.Blocked) {
						this.Train.Sounds.AtsPBell.Play();
					}
				}
			}
			this.SwitchToAtsSxPosition = double.MaxValue;
		}
		
		/// <summary>Switches to ATS-P.</summary>
		/// <param name="state">The desired state.</param>
		private void SwitchToP(States state) {
			if (this.State == States.Standby) {
				if (this.Train.AtsSx == null || this.Train.AtsSx.State != AtsSx.States.Emergency) {
					this.State = state;
					if (!this.Blocked) {
						this.Train.Sounds.AtsPBell.Play();
					}
				}
			} else if (state == States.Service | state == States.Emergency) {
				if (this.State != States.Brake & this.State != States.Service & this.State != States.Emergency) {
					if (!this.Blocked) {
						this.Train.Sounds.AtsPBell.Play();
					}
				}
				this.State = state;
			}
		}
		
		/// <summary>Updates the compatibility temporary speed pattern from the list of known speed limits.</summary>
		private void UpdateCompatibilityTemporarySpeedPattern() {
			if (this.CompatibilityLimits.Count != 0) {
				double oldPosition = 0.0;
				double oldSpeed = 0.0;
				bool restoreOld = false;
				if (this.CompatibilityTemporaryPattern.Position < double.MaxValue) {
					if (this.CompatibilityTemporaryPattern.BrakePattern < this.Train.State.Speed.MetersPerSecond) {
						return;
					}
					double delta = this.CompatibilityTemporaryPattern.Position - this.Train.State.Location;
					if (delta >= -50.0 & delta <= 0.0) {
						oldPosition = this.CompatibilityTemporaryPattern.Position;
						oldSpeed = this.CompatibilityTemporaryPattern.TargetSpeed;
						restoreOld = true;
					}
				}
				if (this.CompatibilityLimitPointer < 0) {
					this.CompatibilityLimitPointer = 0;
				} else if (this.CompatibilityLimitPointer > this.CompatibilityLimits.Count) {
					this.CompatibilityLimitPointer = this.CompatibilityLimits.Count;
				}
				while (this.CompatibilityLimitPointer > 0 && this.CompatibilityLimits[this.CompatibilityLimitPointer - 1].Location > this.Train.State.Location) {
					this.CompatibilityLimitPointer--;
				}
				while (this.CompatibilityLimitPointer < this.CompatibilityLimits.Count && this.CompatibilityLimits[this.CompatibilityLimitPointer].Location <= this.Train.State.Location) {
					this.CompatibilityLimitPointer++;
				}
				if (this.CompatibilityLimitPointer < this.CompatibilityLimits.Count) {
					this.CompatibilityTemporaryPattern.SetLimit(this.CompatibilityLimits[this.CompatibilityLimitPointer].Limit, this.CompatibilityLimits[this.CompatibilityLimitPointer].Location);
				} else {
					this.CompatibilityTemporaryPattern.Clear();
				}
				if (restoreOld) {
					this.CompatibilityTemporaryPattern.Perform(this);
					if (this.CompatibilityTemporaryPattern.BrakePattern > oldSpeed) {
						this.CompatibilityTemporaryPattern.SetLimit(oldSpeed, oldPosition);
					}
				}
			}
		}
		
		
		// --- inherited functions ---
		
		/// <summary>Is called when the system should initialize.</summary>
		/// <param name="mode">The initialization mode.</param>
		internal override void Initialize(InitializationModes mode) {
			if (mode == InitializationModes.OffEmergency) {
				this.State = States.Suppressed;
			} else {
				this.State = States.Standby;
			}
			foreach (var pattern in this.Patterns) {
				if (Math.Abs(this.Train.State.Speed.MetersPerSecond) >= pattern.WarningPattern) {
					pattern.Clear();
				}
			}
		}

		/// <summary>Is called every frame.</summary>
		/// <param name="data">The data.</param>
		/// <param name="blocking">Whether the device is blocked or will block subsequent devices.</param>
		internal override void Elapse(ElapseData data, ref bool blocking) {
			// --- behavior ---
			if (this.CompatibilityLimitsNeedsSort) {
				CompatibilityLimit.Sort(this, this.CompatibilityLimits);
				this.CompatibilityLimitsNeedsSort = false;
			}
			this.Blocked = blocking;
			if (this.State == States.Suppressed) {
				if (data.Handles.BrakeNotch <= this.Train.Specs.BrakeNotches) {
					this.InitializationCountdown = DurationOfInitialization;
					this.State = States.Initializing;
				}
			}
			if (this.State == States.Initializing) {
				this.InitializationCountdown -= data.ElapsedTime.Seconds;
				if (this.InitializationCountdown <= 0.0) {
					this.State = States.Standby;
					this.BrakeRelease = false;
					this.SwitchToAtsSxPosition = double.MaxValue;
					foreach (var pattern in this.Patterns) {
						if (Math.Abs(data.Vehicle.Speed.MetersPerSecond) >= pattern.WarningPattern) {
							pattern.Clear();
						}
					}
					this.Train.Sounds.AtsPBell.Play();
				}
			}
			if (BrakeRelease) {
				BrakeReleaseCountdown -= data.ElapsedTime.Seconds;
				if (BrakeReleaseCountdown <= 0.0) {
					BrakeRelease = false;
					this.Train.Sounds.AtsPBell.Play();
				}
			}
			if (this.State != States.Disabled & this.State != States.Initializing) {
				this.Position += data.Vehicle.Speed.MetersPerSecond * data.ElapsedTime.Seconds;
			}
			if (blocking) {
				if (this.State != States.Disabled & this.State != States.Suppressed) {
					this.State = States.Standby;
				}
			} else {
				if (this.State == States.Normal | this.State == States.Pattern | this.State == States.Brake) {
					bool brake = false;
					bool warning = false;
					bool normal = true;
					if (this.DivergencePattern.Position > double.MinValue & this.DivergencePattern.Position < double.MaxValue) {
						if (Math.Abs(data.Vehicle.Speed.MetersPerSecond) < this.DivergencePattern.BrakePattern) {
							double distance = this.DivergencePattern.Position - this.Position;
							if (distance < -50.0) {
								this.DivergencePattern.Clear();
							}
						}
					}
					this.UpdateCompatibilityTemporarySpeedPattern();
					foreach (var pattern in this.Patterns) {
						pattern.Perform(this);
						if (Math.Abs(data.Vehicle.Speed.MetersPerSecond) >= pattern.WarningPattern - 1.0 / 3.6) {
							normal = false;
						}
						if (Math.Abs(data.Vehicle.Speed.MetersPerSecond) >= pattern.WarningPattern) {
							warning = true;
						}
						if (Math.Abs(data.Vehicle.Speed.MetersPerSecond) >= pattern.BrakePattern) {
							brake = true;
						}
					}
					for (int i = 0; i < this.SignalPatterns.Count; i++) {
						if (this.SignalPatterns[i].Position > double.MinValue & this.SignalPatterns[i].Position < double.MaxValue) {
							if (Math.Abs(data.Vehicle.Speed.MetersPerSecond) < this.DivergencePattern.BrakePattern) {
								double distance = this.DivergencePattern.Position - this.Position;
								if (distance < -50.0) {
									this.Patterns.Remove(this.SignalPatterns[i]);
									this.SignalPatterns[i] = this.SignalPatterns[this.SignalPatterns.Count - 1];
									this.SignalPatterns.RemoveAt(this.SignalPatterns.Count - 1);
									i--;
								}
							}
						}
					}
					if (BrakeRelease) {
						brake = false;
					}
					if (brake & this.State != States.Brake) {
						this.State = States.Brake;
						this.Train.Sounds.AtsPBell.Play();
					} else if (warning & this.State == States.Normal) {
						this.State = States.Pattern;
						this.Train.Sounds.AtsPBell.Play();
					} else if (!brake & !warning & normal & (this.State == States.Pattern | this.State == States.Brake)) {
						this.State = States.Normal;
						this.Train.Sounds.AtsPBell.Play();
					}
					if (this.State == States.Brake) {
						if (data.Handles.BrakeNotch < this.Train.Specs.BrakeNotches) {
							data.Handles.BrakeNotch = this.Train.Specs.BrakeNotches;
						}
					}
					if (this.Position > this.SwitchToAtsSxPosition & this.State != States.Brake & this.State != States.Service & this.State != States.Emergency) {
						SwitchToSx();
					}
				} else if (this.State == States.Service) {
					if (data.Handles.BrakeNotch < this.Train.Specs.BrakeNotches) {
						data.Handles.BrakeNotch = this.Train.Specs.BrakeNotches;
					}
				} else if (this.State == States.Emergency) {
					data.Handles.BrakeNotch = this.Train.Specs.BrakeNotches + 1;
				}
				if (!this.AtsSxPMode & (this.State == States.Normal | this.State == States.Pattern | this.State == States.Brake | this.State == States.Service | this.State == States.Emergency)) {
					blocking = true;
				}
				if (this.State != States.Disabled & this.Train.Doors != DoorStates.None) {
					data.Handles.PowerNotch = 0;
				}
			}
			// --- panel ---
			if (this.State != States.Disabled & this.State != States.Suppressed) {
				this.Train.Panel[2] = 1;
				this.Train.Panel[259] = 1;
			}
			if (this.State == States.Pattern | this.State == States.Brake | this.State == States.Service | this.State == States.Emergency) {
				this.Train.Panel[3] = 1;
				this.Train.Panel[260] = 1;
			}
			if (this.State == States.Brake | this.State == States.Service | this.State == States.Emergency) {
				this.Train.Panel[5] = 1;
				this.Train.Panel[262] = 1;
			}
			if (this.State != States.Disabled & this.State != States.Suppressed & this.State != States.Standby) {
				this.Train.Panel[6] = 1;
				this.Train.Panel[263] = 1;
			}
			if (this.State == States.Initializing) {
				this.Train.Panel[7] = 1;
				this.Train.Panel[264] = 1;
			}
			if (this.State == States.Disabled) {
				this.Train.Panel[50] = 1;
			}
			if (this.State != States.Disabled & this.State != States.Suppressed & this.State != States.Standby & this.BrakeRelease) {
				this.Train.Panel[4] = 1;
				this.Train.Panel[261] = 1;
			}
			// --- debug ---
			if (this.State == States.Normal | this.State == States.Pattern | this.State == States.Brake | this.State == States.Service | this.State == States.Emergency) {
				var builder = new StringBuilder();
				for (int i = 0; i < this.SignalPatterns.Count; i++) {
					this.SignalPatterns[i].AddToStringBuilder(i.ToString() + ":", builder);
				}
				this.DivergencePattern.AddToStringBuilder("分岐/D:", builder);
				this.TemporaryPattern.AddToStringBuilder("臨時/T:", builder);
				this.CurvePattern.AddToStringBuilder("曲線/C:", builder);
				this.DownslopePattern.AddToStringBuilder("勾配/S:", builder);
				this.RoutePermanentPattern.AddToStringBuilder("最高/Max:", builder);
				this.TrainPermanentPattern.AddToStringBuilder("電車/Train:", builder);
				if (this.SwitchToAtsSxPosition != double.MaxValue) {
					if (builder.Length != 0) {
						builder.Append(", ");
					}
					builder.Append("Sx@" + (this.SwitchToAtsSxPosition - this.Position).ToString("0"));
				}
				this.CompatibilityTemporaryPattern.AddToStringBuilder("CompTemp", builder);
				this.CompatibilityPermanentPattern.AddToStringBuilder("CompPerm", builder);
				if (builder.Length == 0) {
					data.DebugMessage = this.State.ToString();
				} else {
					data.DebugMessage = this.State.ToString() + " - " + builder.ToString();
				}
			}
		}
		
		/// <summary>Is called when a key is pressed.</summary>
		/// <param name="key">The key.</param>
		internal override void KeyDown(VirtualKeys key) {
			switch (key) {
				case VirtualKeys.B1:
					// --- reset the system ---
					if ((this.State == States.Brake | this.State == States.Service | this.State == States.Emergency) & this.Train.Handles.Reverser == 0 & this.Train.Handles.PowerNotch == 0 & this.Train.Handles.BrakeNotch >= this.Train.Specs.BrakeNotches) {
						foreach (var pattern in this.Patterns) {
							if (Math.Abs(this.Train.State.Speed.MetersPerSecond) >= pattern.WarningPattern) {
								pattern.Clear();
							}
						}
						this.State = States.Normal;
						this.Train.Sounds.AtsPBell.Play();
					}
					break;
				case VirtualKeys.B2:
					// --- brake release ---
					if ((this.State == States.Normal | this.State == States.Pattern) & !BrakeRelease & DurationOfBrakeRelease > 0.0) {
						BrakeRelease = true;
						BrakeReleaseCountdown = DurationOfBrakeRelease;
						this.Train.Sounds.AtsPBell.Play();
					}
					break;
				case VirtualKeys.E:
					// --- activate or deactivate the system ---
					if (this.State == States.Disabled) {
						this.State = States.Suppressed;
					} else {
						this.State = States.Disabled;
					}
					break;
			}
		}
		
		/// <summary>Is called when a beacon is passed.</summary>
		/// <param name="beacon">The beacon data.</param>
		internal override void SetBeacon(BeaconData beacon) {
			if (this.State != States.Disabled & this.State != States.Suppressed & this.State != States.Initializing) {
				switch (beacon.Type) {
					case 3:
					case 4:
					case 5:
						// --- P signal pattern / P immediate stop ---
						this.Position = this.Train.State.Location;
						if (this.State != States.Service & this.State != States.Emergency) {
							if (this.State == States.Standby & beacon.Optional != -1) {
								SwitchToP(States.Normal);
							}
							if (this.State != States.Standby) {
								double location = this.Train.State.Location + beacon.Signal.Distance;
								int aspect = beacon.Signal.Aspect;
								if (aspect < 0 | aspect >= 10) {
									aspect = 0;
								}
								if (aspect == 0) {
									const double tolerance = 5.0;
									bool add = true;
									for (int i = 0; i < this.SignalPatterns.Count; i++) {
										if (Math.Abs(this.SignalPatterns[i].Position - location) < tolerance) {
											this.SignalPatterns[i].SetRedSignal(location);
											add = false;
										}
									}
									if (add) {
										var pattern = new Pattern(this);
										pattern.SetRedSignal(location);
										this.SignalPatterns.Add(pattern);
										this.Patterns.Add(pattern);
									}
									if (!this.BrakeRelease) {
										if (beacon.Type == 4) {
											SwitchToP(States.Emergency);
										} else if (beacon.Type == 5) {
											SwitchToP(States.Service);
										}
									}
								} else {
									const double tolerance = 5.0;
									for (int i = 0; i < this.SignalPatterns.Count; i++) {
										if (Math.Abs(this.SignalPatterns[i].Position - location) < tolerance) {
											this.Patterns.Remove(this.SignalPatterns[i]);
											this.SignalPatterns[i] = this.SignalPatterns[this.SignalPatterns.Count - 1];
											this.SignalPatterns.RemoveAt(this.SignalPatterns.Count - 1);
											i--;
										}
									}
								}
							}
						}
						break;
					case 6:
						// --- P divergence speed limit ---
						{
							int distance = beacon.Optional / 1000;
							if (distance > 0) {
								if (this.State == States.Standby) {
									SwitchToP(States.Normal);
								}
								this.Position = this.Train.State.Location;
								int speed = beacon.Optional % 1000;
								this.DivergencePattern.SetLimit((double)speed / 3.6, this.Position + distance);
							}
						}
						break;
					case 7:
						// --- P permanent speed limit ---
						this.Position = this.Train.State.Location;
						if (beacon.Optional > 0) {
							if (this.State == States.Standby) {
								SwitchToP(States.Normal);
							}
							this.RoutePermanentPattern.SetLimit((double)beacon.Optional / 3.6, double.MinValue);
						} else {
							SwitchToP(States.Emergency);
						}
						break;
					case 8:
						// --- P downslope speed limit ---
						{
							int distance = beacon.Optional / 1000;
							if (distance > 0) {
								if (this.State == States.Standby) {
									SwitchToP(States.Normal);
								}
								this.Position = this.Train.State.Location;
								int speed = beacon.Optional % 1000;
								this.DownslopePattern.SetLimit((double)speed / 3.6, this.Position + distance);
							}
						}
						break;
					case 9:
						// --- P curve speed limit ---
						{
							int distance = beacon.Optional / 1000;
							if (distance > 0) {
								if (this.State == States.Standby) {
									SwitchToP(States.Normal);
								}
								this.Position = this.Train.State.Location;
								int speed = beacon.Optional % 1000;
								this.CurvePattern.SetLimit((double)speed / 3.6, this.Position + distance);
							}
						}
						break;
					case 10:
						// --- P temporary speed limit / P->S (IIYAMA style) ---
						{
							int left = beacon.Optional / 1000;
							int right = beacon.Optional % 1000;
							if (left != 0) {
								if (this.State == States.Standby) {
									SwitchToP(States.Normal);
								}
								this.Position = this.Train.State.Location;
								this.TemporaryPattern.SetLimit((double)right / 3.6, this.Position + left);
							} else if (left == 0 & right != 0) {
								this.Position = this.Train.State.Location;
								this.SwitchToAtsSxPosition = this.Position + right;
							}
						}
						break;
					case 16:
						// --- P divergence limit released ---
						if (beacon.Optional == 0) {
							this.Position = this.Train.State.Location;
							this.DivergencePattern.Clear();
						}
						break;
					case 18:
						// --- P downslope limit released ---
						if (beacon.Optional == 0) {
							this.Position = this.Train.State.Location;
							this.DownslopePattern.Clear();
						}
						break;
					case 19:
						// --- P curve limit released ---
						if (beacon.Optional == 0) {
							this.Position = this.Train.State.Location;
							this.CurvePattern.Clear();
						}
						break;
					case 20:
						// --- P temporary limit released ---
						if (beacon.Optional == 0) {
							this.Position = this.Train.State.Location;
							this.TemporaryPattern.Clear();
						}
						break;
					case 25:
						// --- P/S system switch ---
						if (beacon.Optional == 0) {
							// --- Sx only ---
							this.Position = this.Train.State.Location;
							if (this.State == States.Normal | this.State == States.Pattern | this.State == States.Brake | this.State == States.Service | this.State == States.Emergency) {
								this.SwitchToAtsSxPosition = this.Position;
							}
						} else if (beacon.Optional == 1) {
							// --- P only ---
							this.Position = this.Train.State.Location;
							if (this.State == States.Standby) {
								SwitchToP(States.Normal);
							}
							if (this.AtsSxPMode) {
								this.AtsSxPMode = false;
								if (this.Train.AtsSx != null & !this.Blocked) {
									this.Train.Sounds.AtsPBell.Play();
								}
							}
						} else if (beacon.Optional == 2) {
							// --- Sx/P ---
							this.Position = this.Train.State.Location;
							if (this.State == States.Standby) {
								SwitchToP(States.Normal);
							}
							if (!this.AtsSxPMode) {
								this.AtsSxPMode = true;
								
								if (this.Train.AtsSx != null & !this.Blocked) {
									this.Train.Sounds.AtsPBell.Play();
								}
							}
						}
						break;
				}
			}
			switch (beacon.Type) {
				case -16777213:
					// --- compatibility temporary pattern ---
					{
						double limit = (double)(beacon.Optional & 4095) / 3.6;
						double position = (double)(beacon.Optional >> 12);
						var item = new CompatibilityLimit(limit, position);
						if (!this.CompatibilityLimits.Contains(item)) {
							this.CompatibilityLimits.Add(item);
							this.CompatibilityLimitsNeedsSort = true;
						}
					}
					break;
				case -16777212:
					// --- compatibility permanent pattern ---
					if (beacon.Optional == 0) {
						this.CompatibilityPermanentPattern.Clear();
					} else {
						double limit = (double)beacon.Optional / 3.6;
						this.CompatibilityPermanentPattern.SetLimit(limit, double.MinValue);
					}
					break;
			}
		}

	}
}
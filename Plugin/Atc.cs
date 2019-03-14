#pragma warning disable 0660, 0661

using System;
using System.Collections.Generic;
using OpenBveApi.Runtime;

namespace Plugin {
	/// <summary>Represents ATC.</summary>
	internal class Atc : Device {
		
		
		// --- enumerations and structures ---
		
		internal enum KirikaeStates {
			Unchanged = 0,
			ToAts = 1,
			ToAtc = 2
		}
		
		/// <summary>Represents different states of ATC.</summary>
		internal enum States {
			/// <summary>The system is disabled.</summary>
			Disabled = 0,
			/// <summary>The system is enabled, but currently suppressed. This will change to States.Ats once the emergency brakes are released.</summary>
			Suppressed = 1,
			/// <summary>The system has been set to ATS mode.</summary>
			Ats = 2,
			/// <summary>The system is operating normally.</summary>
			Normal = 3,
			/// <summary>The system is applying half the service brakes.</summary>
			ServiceHalf = 4,
			/// <summary>The system is applying full service brakes.</summary>
			ServiceFull = 5,
			/// <summary>The system is applying the emergency brakes.</summary>
			Emergency = 6
		}
		
		/// <summary>Represents different states of the compatibility ATC track.</summary>
		private enum CompatibilityStates {
			/// <summary>ATC is not available.</summary>
			Ats = 0,
			/// <summary>ATC is available. The ToAtc reminder plays when the train has come to a stop.</summary>
			ToAtc = 1,
			/// <summary>ATC is available.</summary>
			Atc = 2,
			/// <summary>ATC is available. The ToAts reminder plays when the train has come to a stop.</summary>
			ToAts = 3
		}
		
		/// <summary>Represents different states of the the signal indicator inside the cab.</summary>
		internal enum SignalIndicators {
			/// <summary>The signal shows nothing.</summary>
			None = 0,
			/// <summary>The signal is green.</summary>
			Green = 1,
			/// <summary>The signal is red.</summary>
			Red = 2,
			/// <summary>The signal is red and the P lamp is lit.</summary>
			P = 3,
			/// <summary>The X lamp is lit.</summary>
			X = 4
		}
		
		/// <summary>Represents a signal that was received from the track.</summary>
		internal class Signal {
			// --- members ---
			/// <summary>The aspect underlying this signal, or -1 if not relevant.</summary>
			internal int Aspect;
			/// <summary>What the signal indicator should show for this signal.</summary>
			internal SignalIndicators Indicator;
			/// <summary>The initial speed limit at the beginning of the block, or System.Double.MaxValue to carry over the current speed limit.</summary>
			internal double InitialSpeed;
			/// <summary>The final speed limit at the end of the block, or a negative number for an emergency brake application.</summary>
			internal double FinalSpeed;
			/// <summary>The distance to the end of the block, or a non-positive number to indicate that the final speed should apply immediately, or System.Double.MaxValue if the distance to the end of the block is not known.</summary>
			internal double Distance;
			/// <summary>Whether to switch to ATS.</summary>
			internal KirikaeStates Kirikae;
			/// <summary>Whether to show the advance warning lamp.</summary>
			internal bool ZenpouYokoku;
			/// <summary>Whether to use the overrun protector.</summary>
			internal bool OverrunProtector;
			// --- constructors ---
			/// <summary>Creates a new signal.</summary>
			/// <param name="aspect">The aspect underlying this signal, or -1 if not relevant.</param>
			/// <param name="indicator">What the signal indicator should show for this signal.</param>
			/// <param name="finalSpeed">The final speed limit at the end of the block, or a negative number for an emergency brake application.</param>
			internal Signal(int aspect, SignalIndicators indicator, double finalSpeed) {
				this.Aspect = aspect;
				this.Indicator = indicator;
				this.InitialSpeed = finalSpeed;
				this.FinalSpeed = finalSpeed;
				this.Distance = -1.0;
				this.Kirikae = KirikaeStates.ToAtc;
				this.ZenpouYokoku = false;
				this.OverrunProtector = false;
			}
			/// <summary>Creates a new signal.</summary>
			/// <param name="aspect">The aspect underlying this signal, or -1 if not relevant.</param>
			/// <param name="indicator">What the signal indicator should show for this signal.</param>
			/// <param name="initialSpeed">The initial speed limit at the beginning of the block, or System.Double.MaxValue to carry over the current speed limit.</param>
			/// <param name="finalSpeed">The final speed limit at the end of the block, or a negative number for an emergency brake application.</param>
			/// <param name="distance">The distance to the end of the block, or a non-positive number to indicate that the final speed should apply immediately, or System.Double.MaxValue if the distance to the end of the block is not known.</param>
			internal Signal(int aspect, SignalIndicators indicator, double initialSpeed, double finalSpeed, double distance) {
				this.Aspect = aspect;
				this.Indicator = indicator;
				this.InitialSpeed = initialSpeed;
				this.FinalSpeed = finalSpeed;
				this.Distance = distance;
				this.Kirikae = KirikaeStates.ToAtc;
				this.ZenpouYokoku = false;
				this.OverrunProtector = false;
			}
			/// <summary>Creates a new signal.</summary>
			/// <param name="aspect">The aspect underlying this signal, or -1 if not relevant.</param>
			/// <param name="indicator">What the signal indicator should show for this signal.</param>
			/// <param name="initialSpeed">The initial speed limit at the beginning of the block, or System.Double.MaxValue to carry over the current speed limit.</param>
			/// <param name="finalSpeed">The final speed limit at the end of the block, or a negative number for an emergency brake application.</param>
			/// <param name="distance">The distance to the end of the block, or a non-positive number to indicate that the final speed should apply immediately, or System.Double.MaxValue if the distance to the end of the block is not known.</param>
			/// <param name="kirikae">Whether to switch to ATS.</param>
			/// <param name="zenpouYokoku">Whether to show the advance warning lamp.</param>
			/// <param name="overrunProtector">Whether to use the overrun protector.</param>
			internal Signal(int aspect, SignalIndicators indicator, double initialSpeed, double finalSpeed, double distance, KirikaeStates kirikae, bool zenpouYokoku, bool overrunProtector) {
				this.Aspect = aspect;
				this.Indicator = indicator;
				this.InitialSpeed = initialSpeed;
				this.FinalSpeed = finalSpeed;
				this.Distance = distance;
				this.Kirikae = kirikae;
				this.ZenpouYokoku = zenpouYokoku;
				this.OverrunProtector = overrunProtector;
			}
			/// <summary>Creates a new signal.</summary>
			/// <param name="aspect">The aspect underlying this signal, or -1 if not relevant.</param>
			internal static Signal CreateNoSignal(int aspect) {
				return new Signal(aspect, SignalIndicators.X, -1.0, -1.0, -1.0, KirikaeStates.ToAts, false, false);
			}
			/// <summary>Creates an emergency operation signal.</summary>
			/// <param name="limit">The speed limit, or a negative number for an emergency brake application.</param>
			internal static Signal CreateEmergencyOperationSignal(double limit) {
				return new Signal(-1, SignalIndicators.None, limit, limit, -1.0, KirikaeStates.Unchanged, false, false);
			}
		}
		
		/// <summary>Represents a pattern, containing the signal obtained from the route, as well as the current applicable speed limit.</summary>
		internal class SignalPattern {
			// --- members ---
			/// <summary>The current signal.</summary>
			internal Signal Signal;
			/// <summary>The top speed limit (the current speed limit never exceeds this value).</summary>
			internal double TopSpeed;
			/// <summary>The current speed limit (above which the brakes are engaged).</summary>
			internal double CurrentSpeed;
			/// <summary>The release speed limit (below which the brakes are released).</summary>
			internal double ReleaseSpeed;
			// --- constructors ---
			/// <summary>Creates a new signal pattern, assuming the beginning of the block.</summary>
			internal SignalPattern(Signal signal, Atc atc) {
				this.Signal = signal;
				this.TopSpeed = signal.InitialSpeed;
				this.Update(atc);
			}
			// --- functions ---
			/// <summary>Updates the signal pattern.</summary>
			/// <param name="atc">The ATC device.</param>
			internal void Update(Atc atc) {
				if (this.Signal.Distance == double.MaxValue) {
					this.CurrentSpeed = this.Signal.InitialSpeed;
				} else if (this.Signal.Distance > 0.0) {
					double distance = atc.BlockLocation + this.Signal.Distance - atc.Train.State.Location;
					double deceleration;
					double delay;
					if (this.Signal.OverrunProtector) {
						deceleration = atc.OrpDeceleration;
						delay = atc.OrpDelay;
					} else {
						deceleration = atc.RegularDeceleration;
						delay = atc.RegularDelay;
					}
					double sqrtTerm = 2.0 * deceleration * distance + deceleration * deceleration * delay * delay + this.Signal.FinalSpeed * this.Signal.FinalSpeed;
					if (sqrtTerm > 0.0) {
						this.CurrentSpeed = Math.Sqrt(sqrtTerm) - deceleration * delay;
						if (this.CurrentSpeed > this.Signal.InitialSpeed) {
							this.CurrentSpeed = this.Signal.InitialSpeed;
						} else if (this.CurrentSpeed < this.Signal.FinalSpeed) {
							this.CurrentSpeed = this.Signal.FinalSpeed;
						}
					} else {
						this.CurrentSpeed = this.Signal.FinalSpeed;
					}
					if (distance > 0.0 & this.CurrentSpeed < atc.OrpReleaseSpeed) {
						this.CurrentSpeed = atc.OrpReleaseSpeed;
					}
				} else {
					this.CurrentSpeed = this.Signal.FinalSpeed;
				}
				if (this.CurrentSpeed > this.TopSpeed) {
					this.CurrentSpeed = this.TopSpeed;
				}
				this.ReleaseSpeed = Math.Max(this.Signal.FinalSpeed - 1.0 / 3.6, this.CurrentSpeed - 1.0 / 3.6);
			}
			/// <summary>Checks if two pattern have the same appearance.</summary>
			/// <param name="oldPattern">The first pattern.</param>
			/// <param name="newPattern">The second pattern.</param>
			/// <returns>Whether the patterns have the same apperance.</returns>
			internal static bool ApperanceEquals(SignalPattern oldPattern, SignalPattern newPattern) {
				if (oldPattern.Signal.Indicator != newPattern.Signal.Indicator) return false;
				if (!oldPattern.Signal.ZenpouYokoku & newPattern.Signal.ZenpouYokoku) return false;
				if (oldPattern.Signal.OverrunProtector != newPattern.Signal.OverrunProtector) return false;
				if (newPattern.Signal.OverrunProtector) {
					if (newPattern.CurrentSpeed <= 0.0 & oldPattern.CurrentSpeed > 0.0) {
						return false;
					} else if (Math.Abs(newPattern.CurrentSpeed - oldPattern.CurrentSpeed) > 6.0 / 3.6) {
						return false;
					}
				} else {
					int oc = Math.Min(Math.Max(0, (int)Math.Floor(0.72 * oldPattern.CurrentSpeed + 0.001)), 59);
					int nc = Math.Min(Math.Max(0, (int)Math.Floor(0.72 * newPattern.CurrentSpeed + 0.001)), 59);
					if (oc < nc) return false;
					if (oc > nc + 1) return false;
				}
				int of = Math.Min(Math.Max(0, (int)Math.Floor(0.72 * oldPattern.Signal.FinalSpeed + 0.001)), 59);
				int nf = Math.Min(Math.Max(0, (int)Math.Floor(0.72 * newPattern.Signal.FinalSpeed + 0.001)), 59);
				if (of != nf) return false;
				return true;
			}
		}
		
		/// <summary>Represents a speed limit at a specific track position.</summary>
		private struct CompatibilityLimit {
			// --- members ---
			/// <summary>The speed limit.</summary>
			internal double Limit;
			/// <summary>The track position.</summary>
			internal double Location;
			// --- constructors ---
			/// <summary>Creates a new compatibility limit.</summary>
			/// <param name="limit">The speed limit.</param>
			/// <param name="position">The track position.</param>
			internal CompatibilityLimit(double limit, double location) {
				this.Limit = limit;
				this.Location = location;
			}
		}
		
		
		// --- members ---
		
		/// <summary>The underlying train.</summary>
		private Train Train;
		
		/// <summary>The current state of the system.</summary>
		internal States State;
		
		/// <summary>Whether to switch to ATC in the next Elapse call. This is set by the Initialize call if the train should start in ATC mode. It is necessary to switch in the Elapse call because at the time of the Initialize call, the ATC track status is not yet known.</summary>
		private bool SwitchToAtcOnce;

		/// <summary>Whether emergency operation is enabled. In emergency operation, the train is allowed to travel at 15 km/h (or a custom value) regardless of the actually permitted speed.</summary>
		internal bool EmergencyOperation;
		
		/// <summary>The current signal aspect.</summary>
		private int Aspect;
		
		/// <summary>The location of the beginning of the block.</summary>
		private double BlockLocation;
		
		/// <summary>The current signal pattern.</summary>
		internal SignalPattern Pattern;
		
		/// <summary>The state of the compatibility ATC track.</summary>
		private CompatibilityStates CompatibilityState;
		
		/// <summary>A list of all ATC speed limits in the route.</summary>
		private List<CompatibilityLimit> CompatibilityLimits;
		
		/// <summary>The element in the CompatibilityLimits list that holds the last encountered speed limit.</summary>
		private int CompatibilityLimitPointer;
		
		/// <summary>The last known location of the train at which a non-compatibility ATC signal was received. Is used for built-in ATC suppression.</summary>
		private double CompatibilitySuppressLocation = double.MinValue;
		
		/// <summary>The distance to the last known location of the train at which a non-compatibility ATC signal was received. Within this distance, the built-in ATC is suppressed.</summary>
		private const double CompatibilitySuppressDistance = 50.0;
		
		/// <summary>The state of the preceding train, or a null reference.</summary>
		private PrecedingVehicleState PrecedingTrain;
		
		/// <summary>The upcoming signal aspect.</summary>
		private int RealTimeAdvanceWarningUpcomingSignalAspect = -1;
		
		/// <summary>The location of the upcoming signal.</summary>
		private double RealTimeAdvanceWarningUpcomingSignalLocation = double.MinValue;

		/// <summary>The location of the signal to which the real-time advance warning, set up via beacon type 31, compares.</summary>
		private double RealTimeAdvanceWarningReferenceLocation = double.MinValue;
		
		/// <summary>A timer that determines when to change service brake strength. The timer counts up from 0 to ServiceBrakesTimerMaximum when the change occurs, then the timer is reset to 0.</summary>
		private double ServiceBrakesTimer;
		
		
		// --- constants ---
		
		/// <summary>Represents the signal that indicates that ATC is not available.</summary>
		private readonly Signal NoSignal = Signal.CreateNoSignal(-1);
		
		/// <summary>The compatibility speeds. A value of -1 indicates that ATC is not available.</summary>
		private readonly double[] CompatibilitySpeeds = new double[] {
			-1.0,
			0.0,
			15.0 / 3.6,
			25.0 / 3.6,
			45.0 / 3.6,
			55.0 / 3.6,
			65.0 / 3.6,
			75.0 / 3.6,
			90.0 / 3.6,
			100.0 / 3.6,
			110.0 / 3.6,
			120.0 / 3.6
		};

		
		// --- parameters (brake curve) ---
		
		/// <summary>The deceleration for a maximum service brake application.</summary>
		private double MaximumDeceleration = 4.000 / 3.6;
		
		/// <summary>The deceleration for a regular braking operation.</summary>
		private double RegularDeceleration = 1.910 / 3.6;
		
		/// <summary>The delay for a regular braking operation.</summary>
		private double RegularDelay = 0.5;
		
		/// <summary>The deceleration for the ORP braking operation.</summary>
		private double OrpDeceleration = 4.120 / 3.6;
		
		/// <summary>The delay for the ORP braking operation.</summary>
		private double OrpDelay = 3.9;
		
		/// <summary>The ORP release speed.</summary>
		private double OrpReleaseSpeed = 10 / 3.6;
		
		/// <summary>The acceleration assumed in brake curve calculations.</summary>
		private double Acceleration = 1.910 / 3.6;
		
		/// <summary>The delay for the acceleration assumed in brake curve calculations.</summary>
		private double AccelerationDelay = 2.0;
		
		/// <summary>The small time threshold used in brake curve calculations.</summary>
		private double AccelerationTimeThresholdSmall = 5.0;
		
		/// <summary>The large time threshold used in brake curve calculations.</summary>
		private double AccelerationTimeThresholdLarge = 15.0;
		
		/// <summary>The speed of the final approach to a red signal.</summary>
		private double FinalApproachSpeed = 25.0 / 3.6;
		
		
		// --- parameters (miscellaneous) ---
		
		/// <summary>The time after services brakes switch between half and full.</summary>
		private double ServiceBrakesTimerMaximum = 2.0;
		
		/// <summary>The speed difference between half and full services brakes.</summary>
		private double ServiceBrakesSpeedDifference = 10.0 / 3.6;

		/// <summary>The speed difference to zero between half and full services brakes.</summary>
		private double ServiceBrakesSpeedDifferenceZero = 10.0 / 3.6;
		
		/// <summary>Whether to automatically switch between ATS and ATC.</summary>
		internal bool AutomaticSwitch = false;
		
		/// <summary>Represents the signal for the emergency operation mode, or a null reference if emergency operation is not available.</summary>
		internal Signal EmergencyOperationSignal = Signal.CreateEmergencyOperationSignal(15.0 / 3.6);
		
		/// <summary>The signals recognized by this ATC implementation. The Source parameters must not be null references.</summary>
		internal List<Signal> Signals = new List<Signal>();
		
		
		// --- constructors ---
		
		/// <summary>Creates a new instance of this system.</summary>
		/// <param name="train">The train.</param>
		internal Atc(Train train) {
			this.Train = train;
			this.State = States.Disabled;
			this.EmergencyOperation = false;
			this.Aspect = 0;
			this.Pattern = new SignalPattern(this.NoSignal, this);
			this.CompatibilityState = CompatibilityStates.Ats;
			this.CompatibilityLimits = new List<CompatibilityLimit>();
		}
		
		
		// --- functions ---
		
		/// <summary>Gets the current signal.</summary>
		/// <returns>The signal.</returns>
		private Signal GetCurrentSignal() {
			if (this.EmergencyOperation) {
				return this.EmergencyOperationSignal;
			} else {
				foreach (Signal signal in this.Signals) {
					if (signal.Aspect == this.Aspect) {
						return signal;
					}
				}
				if (Math.Abs(this.CompatibilitySuppressLocation - this.Train.State.Location) > CompatibilitySuppressDistance) {
					if (this.CompatibilityState != CompatibilityStates.Ats) {
						double a = GetAtcSpeedFromTrain();
						double b = GetAtcSpeedFromLimit();
						double limit = Math.Min(a, b);
						if (limit > 0.0) {
							if (this.CompatibilityState == CompatibilityStates.ToAts) {
								return new Signal(-1, SignalIndicators.Red, limit, 0.0, double.MaxValue, Atc.KirikaeStates.ToAts, false, false);
							} else {
								return new Signal(-1, SignalIndicators.Green, limit);
							}
						} else if (limit == 0.0) {
							return new Signal(-1, SignalIndicators.Red, 0.0);
						}
					}
				}
				return this.NoSignal;
			}
		}
		
		/// <summary>Gets the upcoming signal.</summary>
		/// <returns>The signal.</returns>
		private Signal GetUpcomingSignal() {
			if (this.EmergencyOperation) {
				return this.EmergencyOperationSignal;
			} else {
				foreach (Signal signal in this.Signals) {
					if (signal.Aspect == this.RealTimeAdvanceWarningUpcomingSignalAspect) {
						return signal;
					}
				}
				return this.NoSignal;
			}
		}
		
		/// <summary>Gets the ATC speed from the distance to the preceding train if operating in compatibility ATC mode.</summary>
		/// <returns>The ATC speed, or -1 if ATC is not available.</returns>
		private double GetAtcSpeedFromTrain() {
			if (this.CompatibilityState != CompatibilityStates.Ats) {
				if (this.PrecedingTrain == null) {
					return this.CompatibilitySpeeds[11];
				} else {
					const double blockLength = 100.0;
					int a = (int)Math.Floor(this.PrecedingTrain.Location / blockLength);
					int b = (int)Math.Floor(this.Train.State.Location / blockLength);
					int blocks = a - b;
					switch (blocks) {
						case 0:
							return this.CompatibilitySpeeds[0];
						case 1:
							return this.CompatibilitySpeeds[1];
						case 2:
							return this.CompatibilitySpeeds[3];
						case 3:
							return this.CompatibilitySpeeds[4];
						case 4:
							return this.CompatibilitySpeeds[5];
						case 5:
							return this.CompatibilitySpeeds[6];
						case 6:
							return this.CompatibilitySpeeds[7];
						case 7:
							return this.CompatibilitySpeeds[8];
						case 8:
							return this.CompatibilitySpeeds[9];
						case 9:
							return this.CompatibilitySpeeds[10];
						default:
							return this.CompatibilitySpeeds[11];
					}
				}
			} else {
				return -1.0;
			}
		}
		
		/// <summary>Gets the ATC speed from the current and upcoming speed limits.</summary>
		/// <returns>The ATC speed, or -1 if ATC is not available.</returns>
		private double GetAtcSpeedFromLimit() {
			if (this.CompatibilityState != CompatibilityStates.Ats) {
				if (this.CompatibilityLimits.Count == 0) {
					return double.MaxValue;
				} else if (this.CompatibilityLimits.Count == 1) {
					return this.CompatibilityLimits[0].Limit;
				} else {
					while (CompatibilityLimitPointer > 0 && this.CompatibilityLimits[CompatibilityLimitPointer].Location > this.Train.State.Location) {
						CompatibilityLimitPointer--;
					}
					while (CompatibilityLimitPointer < this.CompatibilityLimits.Count - 1 && this.CompatibilityLimits[CompatibilityLimitPointer + 1].Location <= this.Train.State.Location) {
						CompatibilityLimitPointer++;
					}
					if (this.CompatibilityLimitPointer == this.CompatibilityLimits.Count - 1) {
						return this.CompatibilityLimits[this.CompatibilityLimitPointer].Limit;
					} else if (this.CompatibilityLimits[this.CompatibilityLimitPointer].Limit <= this.CompatibilityLimits[this.CompatibilityLimitPointer + 1].Limit) {
						return this.CompatibilityLimits[this.CompatibilityLimitPointer].Limit;
					} else {
						double currentLimit = this.CompatibilityLimits[this.CompatibilityLimitPointer].Limit;
						double upcomingLimit = this.CompatibilityLimits[this.CompatibilityLimitPointer + 1].Limit;
						double distance = (currentLimit * currentLimit - upcomingLimit * upcomingLimit) / (2.0 * this.RegularDeceleration) + this.RegularDelay * currentLimit;
						if (this.Train.State.Location < this.CompatibilityLimits[this.CompatibilityLimitPointer + 1].Location - distance) {
							return this.CompatibilityLimits[this.CompatibilityLimitPointer].Limit;
						} else {
							return this.CompatibilityLimits[this.CompatibilityLimitPointer + 1].Limit;
						}
					}
				}
			} else {
				return -1.0;
			}
		}
		
		/// <summary>Whether the driver should switch to ATS. This returns false if already operating in ATS.</summary>
		/// <returns>Whether the driver should switch to ATS.</returns>
		internal bool ShouldSwitchToAts() {
			if (this.State == States.Normal | this.State == States.ServiceHalf | this.State == Atc.States.ServiceFull | this.State == States.Emergency) {
				if (this.Pattern.Signal.Kirikae == KirikaeStates.ToAts) {
					if (Math.Abs(this.Train.State.Speed.MetersPerSecond) < 1.0 / 3.6) {
						return true;
					}
				}
			}
			return false;
		}
		
		/// <summary>Whether the driver should switch to ATC. This returns false if already operating in ATC.</summary>
		/// <returns>Whether the driver should switch to ATC.</returns>
		internal bool ShouldSwitchToAtc() {
			if (this.State == States.Ats) {
				if (this.Pattern.Signal.Kirikae == KirikaeStates.ToAtc) {
					if (Math.Abs(this.Train.State.Speed.MetersPerSecond) < 1.0 / 3.6) {
						return true;
					}
				}
			}
			return false;
		}
		
		
		// --- inherited functions ---
		
		/// <summary>Is called when the system should initialize.</summary>
		/// <param name="mode">The initialization mode.</param>
		internal override void Initialize(InitializationModes mode) {
			if (mode == InitializationModes.OffEmergency) {
				this.State = States.Suppressed;
			} else {
				this.State = States.Ats;
			}
		}

		/// <summary>Is called every frame.</summary>
		/// <param name="data">The data.</param>
		/// <param name="blocking">Whether the device is blocked or will block subsequent devices.</param>
		internal override void Elapse(ElapseData data, ref bool blocking) {
			// --- internal ---
			this.PrecedingTrain = data.PrecedingVehicle;
			foreach (Signal signal in this.Signals) {
				if (this.Aspect == signal.Aspect) {
					this.CompatibilitySuppressLocation = this.Train.State.Location;
					break;
				}
			}
			// --- behavior ---
			if (this.SwitchToAtcOnce) {
				this.State = States.Normal;
				this.SwitchToAtcOnce = false;
			}
			if (this.State == States.Suppressed) {
				if (data.Handles.BrakeNotch <= this.Train.Specs.BrakeNotches) {
					this.State = States.Ats;
				}
			}
			if (blocking) {
				if (this.State != States.Disabled & this.State != States.Suppressed) {
					this.State = States.Ats;
				}
				this.Pattern.Signal = this.NoSignal;
				this.Pattern.Update(this);
				this.ServiceBrakesTimer = 0.0;
			} else {
				// --- update pattern ---
				Signal newSignal = GetCurrentSignal();
				SignalPattern oldPattern = this.Pattern;
				SignalPattern newPattern = new SignalPattern(newSignal, this);
				if (Math.Abs(this.RealTimeAdvanceWarningUpcomingSignalLocation - this.RealTimeAdvanceWarningReferenceLocation) < 5.0) {
					if (newSignal.FinalSpeed <= 0.0 | newSignal.OverrunProtector) {
						newSignal.ZenpouYokoku = false;
					} else {
						Signal upcomingSignal = GetUpcomingSignal();
						newSignal.ZenpouYokoku = upcomingSignal.FinalSpeed < newSignal.FinalSpeed & newSignal.FinalSpeed > 0.0;
					}
				}
				newPattern.Update(this);
				// --- smooth brake curve and reacceleration prevention ---
				if (newSignal.Distance > 0.0 & newSignal.Distance < double.MaxValue) {
					double actualDistance = this.BlockLocation + newSignal.Distance - this.Train.State.Location;
					double deceleration = newSignal.OverrunProtector ? this.OrpDeceleration : this.RegularDeceleration;
					double decelerationDelay = newSignal.OverrunProtector ? this.OrpDelay : this.RegularDelay;
					double brakingDistance = (oldPattern.CurrentSpeed * oldPattern.CurrentSpeed - newSignal.FinalSpeed * newSignal.FinalSpeed) / (2.0 * deceleration) + oldPattern.CurrentSpeed * decelerationDelay;
					double time = (actualDistance - brakingDistance) / Math.Max(oldPattern.CurrentSpeed, 5.0 / 3.6);
					bool finalApproach = newSignal.FinalSpeed <= 0.0 & newPattern.CurrentSpeed < this.FinalApproachSpeed | newSignal.OverrunProtector;
					if (time > this.AccelerationTimeThresholdLarge & !finalApproach) {
						// --- use a fixed speed ---
						double sqrtTerm = 4.0 * this.Acceleration * this.Acceleration * deceleration * deceleration * (this.AccelerationTimeThresholdLarge + decelerationDelay) * (this.AccelerationTimeThresholdLarge + decelerationDelay) + 4.0 * (this.Acceleration + deceleration) * (deceleration * this.Train.State.Speed.MetersPerSecond * this.Train.State.Speed.MetersPerSecond + this.Acceleration * (2.0 * deceleration * (actualDistance - this.Train.State.Speed.MetersPerSecond * this.AccelerationDelay) + newSignal.FinalSpeed * newSignal.FinalSpeed));
						double speed;
						if (sqrtTerm > 0.0) {
							speed = (-this.Acceleration * deceleration * (this.AccelerationTimeThresholdLarge + decelerationDelay) + 0.5 * Math.Sqrt(sqrtTerm)) / (this.Acceleration + deceleration);
							if (speed < newSignal.FinalSpeed) {
								speed = newSignal.FinalSpeed;
							}
						} else {
							speed = Math.Max(newSignal.FinalSpeed, oldPattern.CurrentSpeed);
						}
						speed = Math.Floor(0.72 * speed + 0.001) / 0.72;
						if (speed < oldPattern.CurrentSpeed) {
							speed = oldPattern.CurrentSpeed;
						}
						newSignal = new Signal(newSignal.Aspect, newSignal.Indicator, speed, speed, -1.0, newSignal.Kirikae, newSignal.ZenpouYokoku, newSignal.OverrunProtector);
						newPattern = new SignalPattern(newSignal, this);
						newPattern.Update(this);
					} else if (time > this.AccelerationTimeThresholdSmall & !finalApproach) {
						// --- use fixed speed or brake curve, whichever was there before ---
						if (oldPattern.Signal.Distance > 0.0 & oldPattern.Signal.Distance < double.MaxValue) {
							newPattern.TopSpeed = Math.Max(newSignal.FinalSpeed, oldPattern.CurrentSpeed);
							newPattern.Update(this);
						} else {
							double speed = oldPattern.CurrentSpeed;
							newSignal = new Signal(oldPattern.Signal.Aspect, oldPattern.Signal.Indicator, speed, speed, -1.0, oldPattern.Signal.Kirikae, oldPattern.Signal.ZenpouYokoku, oldPattern.Signal.OverrunProtector);
							newPattern = new SignalPattern(newSignal, this);
							newPattern.Update(this);
						}
					} else {
						// --- use a brake curve ---
						if (finalApproach & newSignal.Indicator == SignalIndicators.Green) {
							newSignal = new Signal(newSignal.Aspect, SignalIndicators.Red, newSignal.InitialSpeed, newSignal.FinalSpeed, newSignal.Distance, newSignal.Kirikae, newSignal.ZenpouYokoku, newSignal.OverrunProtector);
							newPattern = new SignalPattern(newSignal, this);
						}
						if (!newSignal.OverrunProtector) {
							newPattern.TopSpeed = Math.Max(newSignal.FinalSpeed, oldPattern.CurrentSpeed);
						}
						newPattern.Update(this);
					}
				}
				// --- apply pattern ---
				if (this.State == States.Normal | this.State == States.ServiceHalf | this.State == Atc.States.ServiceFull | this.State == States.Emergency) {
					if (!SignalPattern.ApperanceEquals(oldPattern, newPattern)) {
						this.Train.Sounds.AtcBell.Play();
					}
				}
				this.Pattern = newPattern;
				// --- switch states and apply brakes ---
				if (this.State == States.Normal | this.State == States.ServiceHalf | this.State == Atc.States.ServiceFull | this.State == States.Emergency) {
					// --- switch states ---
					if (this.Pattern.CurrentSpeed < 0.0 || this.Pattern.Signal.OverrunProtector && (Math.Abs(data.Vehicle.Speed.MetersPerSecond) >= this.Pattern.CurrentSpeed || this.State == States.Emergency && (Math.Abs(data.Vehicle.Speed.MetersPerSecond) > 0.0 || this.ServiceBrakesTimer < this.ServiceBrakesTimerMaximum))) {
						// --- emergency ---
						if (this.State != States.ServiceFull & this.State != States.Emergency) {
							this.ServiceBrakesTimer = 0.0;
						}
						if (this.State != States.Emergency) {
							this.Train.Sounds.AtcBell.Play();
							this.State = States.Emergency;
						}
						if (this.Pattern.Signal.OverrunProtector & Math.Abs(data.Vehicle.Speed.MetersPerSecond) > 0.0) {
							this.ServiceBrakesTimer = 0.0;
						} else if (this.ServiceBrakesTimer < this.ServiceBrakesTimerMaximum) {
							this.ServiceBrakesTimer += data.ElapsedTime.Seconds;
						}
//					} else if (this.Pattern.CurrentSpeed == 0.0) {
//						// --- service (0 km/h) ---
//						if (this.State != States.ServiceFull & this.State != States.Emergency) {
//							this.ServiceBrakesTimer = 0.0;
//						}
//						if (this.State == States.Emergency) {
//							this.Train.Sounds.AtcBell.Play();
//						}
//						this.State = States.ServiceFull;
//						if (this.ServiceBrakesTimer < this.ServiceBrakesTimerMaximum) {
//							this.ServiceBrakesTimer += data.ElapsedTime.Seconds;
//						}
					} else if (Math.Abs(data.Vehicle.Speed.MetersPerSecond) < this.Pattern.ReleaseSpeed && this.Pattern.CurrentSpeed > 0.0) {
						// --- normal ---
						if (this.State == States.Emergency) {
							this.Train.Sounds.AtcBell.Play();
							this.State = States.Normal;
							this.ServiceBrakesTimer = 0.0;
						}
						if (this.ServiceBrakesTimer < this.ServiceBrakesTimerMaximum) {
							this.ServiceBrakesTimer += data.ElapsedTime.Seconds;
						}
						if (this.ServiceBrakesTimer >= this.ServiceBrakesTimerMaximum) {
							if (this.State == States.ServiceFull | this.State == States.Emergency) {
								this.State = States.ServiceHalf;
								this.ServiceBrakesTimer = 0.0;
							} else if (this.State == States.ServiceHalf) {
								this.State = States.Normal;
								this.ServiceBrakesTimer = 0.0;
							}
						}
					} else if (Math.Abs(data.Vehicle.Speed.MetersPerSecond) >= this.Pattern.CurrentSpeed + 1.0 / 3.6 || this.Pattern.CurrentSpeed <= 0.0) {
						// --- service ---
						if (this.State == States.Emergency) {
							this.Train.Sounds.AtcBell.Play();
							this.State = States.ServiceFull;
							this.ServiceBrakesTimer = 0.0;
						}
						if (this.Train.State.Speed.MetersPerSecond < Math.Max(this.Pattern.CurrentSpeed, 0.0) + (this.Pattern.CurrentSpeed <= 0.0 ? ServiceBrakesSpeedDifferenceZero : ServiceBrakesSpeedDifference)) {
							// --- half service ---
							if (this.ServiceBrakesTimer < this.ServiceBrakesTimerMaximum) {
								this.ServiceBrakesTimer += data.ElapsedTime.Seconds;
							}
							if (this.ServiceBrakesTimer >= this.ServiceBrakesTimerMaximum) {
								if (this.State != States.ServiceHalf) {
									this.State = States.ServiceHalf;
									this.ServiceBrakesTimer = 0.0;
								}
							}
						} else {
							// --- full service ---
							if (this.ServiceBrakesTimer < this.ServiceBrakesTimerMaximum) {
								this.ServiceBrakesTimer += data.ElapsedTime.Seconds;
							}
							if (this.ServiceBrakesTimer >= this.ServiceBrakesTimerMaximum) {
								if (this.State == States.Normal) {
									this.State = States.ServiceHalf;
									this.ServiceBrakesTimer = 0.0;
								} else if (this.State == States.ServiceHalf) {
									this.State = States.ServiceFull;
									this.ServiceBrakesTimer = 0.0;
								}
							}
						}
					}
					// --- apply brakes ---
					if (this.State == States.ServiceHalf) {
						double deceleration = this.Pattern.Signal.OverrunProtector ? this.OrpDeceleration : this.RegularDeceleration;
						int notch = (int)Math.Round((double)(this.Train.Specs.BrakeNotches - this.Train.Specs.AtsNotch + 1) * (deceleration / this.MaximumDeceleration));
						notch += this.Train.Specs.AtsNotch - 1;
						if (notch > this.Train.Specs.BrakeNotches) {
							notch = this.Train.Specs.BrakeNotches;
						}
						if (data.Handles.BrakeNotch < notch) {
							data.Handles.BrakeNotch = notch;
						}
					} else if (this.State == States.ServiceFull) {
						data.Handles.BrakeNotch = this.Train.Specs.BrakeNotches;
					} else if (this.State == States.Emergency) {
						data.Handles.BrakeNotch = this.Train.Specs.BrakeNotches + 1;
					}
					blocking = true;
				} else {
					this.ServiceBrakesTimer = 0.0;
				}
				if (this.State != States.Disabled & this.Train.Doors != DoorStates.None) {
					data.Handles.PowerNotch = 0;
				}
			}
			// --- panel ---
			if (this.State == States.Ats) {
				this.Train.Panel[21] = 1;
				this.Train.Panel[271] = 12;
			} else if (this.State == States.Normal | this.State == States.ServiceHalf | this.State == States.ServiceFull | this.State == States.Emergency) {
				this.Train.Panel[15] = 1;
				this.Train.Panel[265] = 1;
				if (this.Pattern.Signal.Indicator == SignalIndicators.X) {
					this.Train.Panel[22] = 1;
					this.Train.Panel[271] = 0;
				} else {
					int start = 1;
					for (int i = 11; i >= 1; i--) {
						if (this.Pattern.Signal.FinalSpeed >= this.CompatibilitySpeeds[i] - 0.001) {
							start = i;
							break;
						}
					}
					int end = 1;
					for (int i = 11; i >= 1; i--) {
						if (this.Pattern.CurrentSpeed >= this.CompatibilitySpeeds[i] - 0.001) {
							end = i;
							break;
						}
					}
					#if false
					for (int i = start; i <= end; i++) {
						this.Train.Panel[22 + i] = 1;
					}
					#else
					this.Train.Panel[22 + start] = 1;
					#endif
					this.Train.Panel[271] = start;
				}
				switch (this.Pattern.Signal.Indicator) {
					case SignalIndicators.Red:
						this.Train.Panel[110] = 1;
						break;
					case SignalIndicators.Green:
						this.Train.Panel[111] = 1;
						break;
					case SignalIndicators.X:
						this.Train.Panel[22] = 1;
						break;
				}
				if (this.Pattern.Signal.OverrunProtector) {
					this.Train.Panel[112] = 1;
				}
				if (this.Pattern.Signal.ZenpouYokoku) {
					this.Train.Panel[113] = 1;
				}
				this.Train.Panel[34] = (int)Math.Round(3600.0 * Math.Max(0.0, this.Pattern.CurrentSpeed));
				if (this.Pattern.Signal.OverrunProtector) {
					this.Train.Panel[114] = (int)Math.Round(3600.0 * Math.Max(0.0, this.Pattern.CurrentSpeed));
				} else if (this.Pattern.Signal.Indicator != SignalIndicators.X) {
					int currentIndex = Math.Min(Math.Max(0, (int)Math.Floor(0.72 * this.Pattern.CurrentSpeed + 0.001)), 59);
					int targetIndex = Math.Min(Math.Max(0, (int)Math.Floor(0.72 * this.Pattern.Signal.FinalSpeed + 0.001)), 59);
					for (int i = targetIndex; i <= currentIndex; i++) {
						this.Train.Panel[120 + i] = 1;
					}
				}
			}
			if (this.State == States.ServiceHalf | this.State == States.ServiceFull) {
				this.Train.Panel[16] = 1;
				this.Train.Panel[267] = 1;
			} else if (this.State == States.Emergency) {
				this.Train.Panel[17] = 1;
				this.Train.Panel[268] = 1;
			}
			if (this.State != States.Disabled & this.State != States.Suppressed) {
				this.Train.Panel[18] = 1;
				this.Train.Panel[266] = 1;
			}
			if (this.EmergencyOperation) {
				this.Train.Panel[19] = 1;
				this.Train.Panel[52] = 1;
			}
			if (this.State == States.Disabled) {
				this.Train.Panel[20] = 1;
				this.Train.Panel[53] = 1;
			}
			// --- manual or automatic switch ---
			if (ShouldSwitchToAts()) {
				if (this.AutomaticSwitch & Math.Abs(data.Vehicle.Speed.MetersPerSecond) < 1.0 / 3.6) {
					KeyDown(VirtualKeys.C1);
				} else {
					this.Train.Sounds.ToAtsReminder.Play();
				}
			} else if (ShouldSwitchToAtc()) {
				if (this.AutomaticSwitch & Math.Abs(data.Vehicle.Speed.MetersPerSecond) < 1.0 / 3.6) {
					KeyDown(VirtualKeys.C2);
				} else {
					this.Train.Sounds.ToAtcReminder.Play();
				}
			}
			// --- debug ---
			if (this.State == States.Normal | this.State == States.ServiceHalf | this.State == States.ServiceFull | this.State == States.Emergency) {
				data.DebugMessage = this.State.ToString() + " - A:" + this.Pattern.Signal.Aspect + " I:" + (this.Pattern.Signal.InitialSpeed < double.MaxValue ? (3.6 * this.Pattern.Signal.InitialSpeed).ToString("0") : "∞") + " F:" + (3.6 * this.Pattern.Signal.FinalSpeed).ToString("0") + " D=" + (this.Pattern.Signal.Distance == double.MaxValue ? "∞" : (this.Pattern.Signal.Distance - (this.Train.State.Location - this.BlockLocation)).ToString("0")) + " T:" + (3.6 * this.Pattern.TopSpeed).ToString("0") + " C:" + (3.6 * this.Pattern.CurrentSpeed).ToString("0");
			}
		}
		
		/// <summary>Is called when the driver changes the reverser.</summary>
		/// <param name="reverser">The new reverser position.</param>
		internal override void SetReverser(int reverser) {
		}
		
		/// <summary>Is called when the driver changes the power notch.</summary>
		/// <param name="powerNotch">The new power notch.</param>
		internal override void SetPower(int powerNotch) {
		}
		
		/// <summary>Is called when the driver changes the brake notch.</summary>
		/// <param name="brakeNotch">The new brake notch.</param>
		internal override void SetBrake(int brakeNotch) {
		}
		
		/// <summary>Is called when a key is pressed.</summary>
		/// <param name="key">The key.</param>
		internal override void KeyDown(VirtualKeys key) {
			switch (key) {
				case VirtualKeys.C1:
					// --- switch to ats ---
					if (this.State == States.Normal | this.State == States.ServiceHalf | this.State == States.ServiceFull | this.State == States.Emergency) {
						this.State = States.Ats;
						if (!ShouldSwitchToAtc()) {
							this.Train.Sounds.ToAts.Play();
						}
						this.Train.Sounds.AtcBell.Play();
					}
					break;
				case VirtualKeys.C2:
					// --- switch to atc ---
					if (this.State == States.Ats) {
						this.State = States.Normal;
						if (!ShouldSwitchToAts()) {
							this.Train.Sounds.ToAtc.Play();
						}
						this.Train.Sounds.AtcBell.Play();
					}
					break;
				case VirtualKeys.G:
					// --- activate or deactivate the system ---
					if (this.State == States.Disabled) {
						this.State = States.Suppressed;
					} else {
						this.State = States.Disabled;
					}
					break;
				case VirtualKeys.H:
					// --- enable or disable emergency operation mode ---
					if (this.EmergencyOperationSignal != null) {
						this.EmergencyOperation = !this.EmergencyOperation;
						if (this.State == States.Normal | this.State == States.ServiceHalf | this.State == States.ServiceFull | this.State == States.Emergency) {
							//this.Train.Sounds.AtcBell.Play();
						}
					}
					break;
			}
		}
		
		/// <summary>Is called when a key is released.</summary>
		/// <param name="key">The key.</param>
		internal override void KeyUp(VirtualKeys key) {
		}
		
		/// <summary>Is called when a horn is played or when the music horn is stopped.</summary>
		/// <param name="type">The type of horn.</param>
		internal override void HornBlow(HornTypes type) {
		}
		
		/// <summary>Is called to inform about signals.</summary>
		/// <param name="signal">The signal data.</param>
		internal override void SetSignal(SignalData[] signal) {
			this.BlockLocation = this.Train.State.Location + signal[0].Distance;
			this.Aspect = signal[0].Aspect;
			if (signal.Length >= 2) {
				this.RealTimeAdvanceWarningUpcomingSignalAspect = signal[1].Aspect;
				this.RealTimeAdvanceWarningUpcomingSignalLocation = this.Train.State.Location + signal[1].Distance;
			} else {
				this.RealTimeAdvanceWarningUpcomingSignalAspect = -1;
				this.RealTimeAdvanceWarningUpcomingSignalLocation = double.MaxValue;
			}
		}
		
		/// <summary>Is called when a beacon is passed.</summary>
		/// <param name="beacon">The beacon data.</param>
		internal override void SetBeacon(BeaconData beacon) {
			switch (beacon.Type) {
				case 31:
					// --- advance warning ---
					if (beacon.Signal.Distance > 0.0 & beacon.Optional == 0) {
						this.RealTimeAdvanceWarningReferenceLocation = this.Train.State.Location + beacon.Signal.Distance;
					}
					break;
				case -16777215:
					if (beacon.Optional >= 0 & beacon.Optional <= 3) {
						this.CompatibilityState = (CompatibilityStates)beacon.Optional;
					}
					break;
				case -16777214:
					{
						double limit = (double)(beacon.Optional & 4095) / 3.6;
						double location = (beacon.Optional >> 12);
						CompatibilityLimit item = new CompatibilityLimit(limit, location);
						if (!this.CompatibilityLimits.Contains(item)) {
							this.CompatibilityLimits.Add(item);
						}
					}
					break;
			}
		}

	}
}
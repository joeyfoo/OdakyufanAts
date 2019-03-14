using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using OpenBveApi.Runtime;

namespace Plugin {
	/// <summary>Represents a train that is simulated by this plugin.</summary>
	internal class Train {
		
		
		// --- classes and enumerations ---
		
		/// <summary>Represents handles that can only be read from.</summary>
		internal class ReadOnlyHandles {
			// --- members ---
			/// <summary>The reverser position.</summary>
			private int MyReverser;
			/// <summary>The power notch.</summary>
			private int MyPowerNotch;
			/// <summary>The brake notch.</summary>
			private int MyBrakeNotch;
			/// <summary>Whether the const speed system is enabled.</summary>
			private bool MyConstSpeed;
			// --- properties ---
			/// <summary>Gets or sets the reverser position.</summary>
			internal int Reverser {
				get {
					return this.MyReverser;
				}
			}
			/// <summary>Gets or sets the power notch.</summary>
			internal int PowerNotch {
				get {
					return this.MyPowerNotch;
				}
			}
			/// <summary>Gets or sets the brake notch.</summary>
			internal int BrakeNotch {
				get {
					return this.MyBrakeNotch;
				}
			}
			/// <summary>Gets or sets whether the const speed system is enabled.</summary>
			internal bool ConstSpeed {
				get {
					return this.MyConstSpeed;
				}
			}
			// --- constructors ---
			/// <summary>Creates a new instance of this class.</summary>
			/// <param name="handles">The handles</param>
			internal ReadOnlyHandles(Handles handles) {
				this.MyReverser = handles.Reverser;
				this.MyPowerNotch = handles.PowerNotch;
				this.MyBrakeNotch = handles.BrakeNotch;
				this.MyConstSpeed = handles.ConstSpeed;
			}
		}
		
		private class Signal {
			// --- members ---
			internal double Location;
			internal int Aspect;
			// --- constructors ---
			internal Signal(double location, int aspect) {
				this.Location = location;
				this.Aspect = aspect;
			}
		}
		
		
		// --- plugin ---
		
		/// <summary>Whether the plugin is currently initializing. This happens in-between Initialize and Elapse calls, for example when jumping to a station from the menu.</summary>
		internal bool PluginInitializing;
		
		
		// --- train ---

		/// <summary>The train specifications.</summary>
		internal VehicleSpecs Specs;
		
		/// <summary>The current state of the train.</summary>
		internal VehicleState State;
		
		/// <summary>The driver handles at the last Elapse call.</summary>
		internal ReadOnlyHandles Handles;
		
		/// <summary>The current state of the doors.</summary>
		internal DoorStates Doors;
		
		
		// --- acceleration ---
		
		/// <summary>The current value of the accelerometer.</summary>
		internal double Acceleration;
		
		/// <summary>The speed of the train at the beginning of the accelerometer timer.</summary>
		private double AccelerometerSpeed;
		
		/// <summary>The time elapsed since the last reset of the accelerometer timer.</summary>
		private double AccelerometerTimer;
		
		/// <summary>The maximum value for the accelerometer timer.</summary>
		private const double AccelerometerMaximumTimer = 0.25;
		
		
		// --- continuous analog transmission ---
		
		/// <summary>Whether continuous analog tranmission is currently available.</summary>
		private bool ContinuousAnalogTransmissionAvailable;
		
		/// <summary>The position of the signal continuous analog tranmission is engaged to.</summary>
		private double ContinuousAnalogTransmissionSignalLocation;
		
		/// <summary>The signal aspect continuous analog tranmission is engaged to.</summary>
		private int ContinuousAnalogTransmissionSignalAspect;
		
		/// <summary>The active frequency continuous analog tranmission is engaged to.</summary>
		private int ContinuousAnalogTransmissionActiveFrequency;
		
		/// <summary>The idle frequency continuous analog tranmission is engaged to.</summary>
		private int ContinuousAnalogTransmissionIdleFrequency;
		
		/// <summary>The last frequency transmitted continuously.</summary>
		private int ContinuousAnalogTransmissionLastFrequency;

		
		// --- signalling ---
		
		/// <summary>A list of known signal locations and aspects.</summary>
		private Signal[] KnownSignals;

		
		// --- panel and sound ---
		
		/// <summary>The panel variables.</summary>
		internal int[] Panel;
		
		/// <summary>Whether illumination in the panel is enabled.</summary>
		internal bool PanelIllumination;

		/// <summary>The sounds used on this train.</summary>
		internal Sounds Sounds;
		
		/// <summary>Remembers which of the virtual keys are currently pressed down.</summary>
		private bool[] KeysPressed = new bool[16];
		
		
		// --- AI ---
		
		/// <summary>The AI component that drives the train.</summary>
		internal AI AI;
		
		/// <summary>The AI component that calls out signal aspects and speed restrictions.</summary>
		internal Calling Calling;
		
		
		// --- devices ---
		
		/// <summary>The ATS-Sx device, or a null reference if not installed.</summary>
		internal AtsSx AtsSx;
		
		/// <summary>The ATS-Ps device, or a null reference if not installed.</summary>
		internal AtsPs AtsPs;
		
		/// <summary>The ATS-P device, or a null reference if not installed.</summary>
		internal AtsP AtsP;
		
		/// <summary>The ATC device, or a null reference if not installed.</summary>
		internal Atc Atc;

		/// <summary>The EB device, or a null reference if not installed.</summary>
		internal Eb Eb;

		/// <summary>The TASC device, or a null reference if not installed.</summary>
		internal Tasc Tasc;

		/// <summary>The ATO device, or a null reference if not installed.</summary>
		internal Ato Ato;

		/// <summary>A list of all the devices installed on this train. The devices must be in the order ATO, TASC, EB, ATC, ATS-P, ATS-Ps and ATS-Sx.</summary>
		internal Device[] Devices;
		
		
		// --- constructors ---

		/// <summary>Creates a new train without any devices installed.</summary>
		/// <param name="panel">The array of panel variables.</param>
		/// <param name="playSound">The delegate to play sounds.</param>
		internal Train(int[] panel, PlaySoundDelegate playSound) {
			this.PluginInitializing = false;
			this.Specs = new VehicleSpecs(0, BrakeTypes.ElectromagneticStraightAirBrake, 0, false, 0);
			this.State = new VehicleState(0.0, new Speed(0.0), 0.0, 0.0, 0.0, 0.0, 0.0);
			this.Handles = new ReadOnlyHandles(new Handles(0, 0, 0, false));
			this.Doors = DoorStates.None;
			this.KnownSignals = new Signal[] { };
			this.Panel = panel;
			this.Sounds = new Sounds(playSound);
			this.AI = new AI(this);
			this.Calling = new Calling(this, playSound);
		}
		
		
		// --- functions ---
		
		/// <summary>Sets up the devices from the specified configuration file.</summary>
		/// <param name="file">The configuration file.</param>
		internal void LoadConfigurationFile(string file) {
			string[] lines = File.ReadAllLines(file, Encoding.UTF8);
			string section = string.Empty;
			for (int i = 0; i < lines.Length; i++) {
				string line = lines[i];
				int semicolon = line.IndexOf(';');
				if (semicolon >= 0) {
					line = line.Substring(0, semicolon).Trim();
				} else {
					line = line.Trim();
				}
				if (line.Length != 0) {
					if (line[0] == '[' & line[line.Length - 1] == ']') {
						section = line.Substring(1, line.Length - 2).ToLowerInvariant();
						switch (section) {
							case "ats-sx":
								this.AtsSx = new AtsSx(this);
								break;
							case "ats-ps":
								this.AtsPs = new AtsPs(this);
								break;
							case "ats-p":
								this.AtsP = new AtsP(this);
								break;
							case "atc":
								this.Atc = new Atc(this);
								break;
							case "eb":
								this.Eb = new Eb(this);
								break;
							case "tasc":
								this.Tasc = new Tasc(this);
								break;
							case "ato":
								this.Ato = new Ato(this);
								break;
							default:
								throw new InvalidDataException("The section " + line[0] + " is not supported.");
						}
					} else {
						int equals = line.IndexOf('=');
						if (equals >= 0) {
							string key = line.Substring(0, equals).Trim().ToLowerInvariant();
							string value = line.Substring(equals + 1).Trim();
							switch (section) {
								case "ats-sx":
									switch (key) {
										case "durationofalarm":
											this.AtsSx.DurationOfAlarm = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "durationofinitialization":
											this.AtsSx.DurationOfInitialization = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "durationofspeedcheck":
											this.AtsSx.DurationOfSpeedCheck = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										default:
											throw new InvalidDataException("The parameter " + key + " is not supported.");
									}
									break;
								case "ats-ps":
									switch (key) {
										case "maximumspeed":
											this.AtsPs.TrainPermanentPattern.SetPersistentLimit((1.0 / 3.6) * double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
											break;
										default:
											throw new InvalidDataException("The parameter " + key + " is not supported.");
									}
									break;
								case "ats-p":
									switch (key) {
										case "durationofinitialization":
											this.AtsP.DurationOfInitialization = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "durationofbrakerelease":
											this.AtsP.DurationOfBrakeRelease = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "designdeceleration":
											this.AtsP.DesignDeceleration = (1.0 / 3.6) * double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "brakepatterndelay":
											this.AtsP.BrakePatternDelay = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "brakepatternoffset":
										case "signaloffset":
											this.AtsP.BrakePatternOffset = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "brakepatterntolerance":
											this.AtsP.BrakePatternTolerance = (1.0 / 3.6) * double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "warningpatterndelay":
										case "reactiondelay":
											this.AtsP.WarningPatternDelay = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "warningpatternoffset":
											this.AtsP.WarningPatternOffset = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "warningpatterntolerance":
											this.AtsP.WarningPatternTolerance = (1.0 / 3.6) * double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "patternspeeddifference":
											this.AtsP.WarningPatternTolerance = (-1.0 / 3.6) * double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "releasespeed":
											this.AtsP.ReleaseSpeed = (1.0 / 3.6) * double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "maximumspeed":
											this.AtsP.TrainPermanentPattern.SetPersistentLimit((1.0 / 3.6) * double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
											break;
										default:
											throw new InvalidDataException("The parameter " + key + " is not supported.");
									}
									break;
								case "atc":
									switch (key) {
										case "automaticswitch":
											this.Atc.AutomaticSwitch = value.Equals("true", StringComparison.OrdinalIgnoreCase);
											break;
										case "emergencyoperation":
											if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) {
												this.Atc.EmergencyOperationSignal = null;
											} else {
												double limit = (1.0 / 3.6) * double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
												if (limit <= 0.0) {
													this.Atc.EmergencyOperationSignal = null;
												} else {
													this.Atc.EmergencyOperationSignal = Atc.Signal.CreateEmergencyOperationSignal(limit);
												}
											}
											break;
										default:
											int aspect;
											if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out aspect)) {
												if (aspect >= 10) {
													Atc.Signal signal = ParseAtcCode(aspect, value);
													if (signal != null) {
														this.Atc.Signals.Add(signal);
														break;
													} else {
														throw new InvalidDataException("The ATC code " + value + " is not supported.");
													}
												}
											}
											throw new InvalidDataException("The parameter " + key + " is not supported.");
									}
									break;
								case "eb":
									switch (key) {
										case "timeuntilbell":
											this.Eb.TimeUntilBell = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "timeuntilbrake":
											this.Eb.TimeUntilBrake = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										case "speedthreshold":
											this.Eb.SpeedThreshold = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										default:
											throw new InvalidDataException("The parameter " + key + " is not supported.");
									}
									break;
								case "tasc":
									switch (key) {
										case "designdeceleration":
											this.Tasc.TascBrakeDeceleration = (1.0 / 3.6) * double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
											break;
										default:
											throw new InvalidDataException("The parameter " + key + " is not supported.");
									}
									break;
							}
						}
					}
				}
			}
			// --- devices ---
			List<Device> devices = new List<Device>();
			if (this.Ato != null) {
				devices.Add(this.Ato);
			}
			if (this.Tasc != null) {
				devices.Add(this.Tasc);
			}
			if (this.Eb != null) {
				devices.Add(this.Eb);
			}
			if (this.Atc != null) {
				devices.Add(this.Atc);
			}
			if (this.AtsP != null) {
				devices.Add(this.AtsP);
			}
			if (this.AtsPs != null) {
				devices.Add(this.AtsPs);
				if (this.AtsSx == null) {
					this.AtsSx = new AtsSx(this);
				}
			}
			if (this.AtsSx != null) {
				devices.Add(this.AtsSx);
			}
			this.Devices = devices.ToArray();
		}
		
		/// <summary>Parses an ATC code and returns the corresponding signal.</summary>
		/// <param name="aspect">The aspect.</param>
		/// <param name="code">The code.</param>
		/// <returns>The signal corresponding to the code, or a null reference if the code is invalid.</returns>
		private Atc.Signal ParseAtcCode(int aspect, string code) {
			if (code == "S01") {
				return new Atc.Signal(aspect, Atc.SignalIndicators.Red, 0.0);
			} else if (code == "01") {
				return new Atc.Signal(aspect, Atc.SignalIndicators.Red, 0.0);
			} else if (code == "S02E") {
				return new Atc.Signal(aspect, Atc.SignalIndicators.Red, -1.0);
			} else if (code == "02E") {
				return new Atc.Signal(aspect, Atc.SignalIndicators.Red, -1.0);
			} else if (code == "02") {
				return Atc.Signal.CreateNoSignal(aspect);
			} else if (code == "03") {
				return new Atc.Signal(aspect, Atc.SignalIndicators.Red, -1.0);
			} else if (code == "ATS") {
				return new Atc.Signal(aspect, Atc.SignalIndicators.Red, double.MaxValue, 0.0, double.MaxValue, Atc.KirikaeStates.ToAts, false, false);
			} else {
				Atc.SignalIndicators indicator;
				bool kirikae = false;
				bool zenpouYokoku = false;
				bool overrunProtector = false;
				// --- prefix ---
				if (code.StartsWith("ATS")) {
					indicator = Atc.SignalIndicators.Red;
					kirikae = true;
					code = code.Substring(3);
				} else if (code.StartsWith("K")) {
					indicator = Atc.SignalIndicators.Red;
					kirikae = true;
					code = code.Substring(1);
				} else if (code.StartsWith("P")) {
					indicator = Atc.SignalIndicators.P;
					code = code.Substring(1);
				} else if (code.StartsWith("R")) {
					indicator = Atc.SignalIndicators.Red;
					code = code.Substring(1);
				} else if (code.StartsWith("SY")) {
					indicator = Atc.SignalIndicators.Red;
					zenpouYokoku = true;
					code = code.Substring(2);
				} else if (code.StartsWith("Y")) {
					indicator = Atc.SignalIndicators.Green;
					zenpouYokoku = true;
					code = code.Substring(1);
				} else if (code.StartsWith("S")) {
					indicator = Atc.SignalIndicators.Red;
					code = code.Substring(1);
				} else if (code.StartsWith("G")) {
					indicator = Atc.SignalIndicators.Green;
					code = code.Substring(1);
				} else {
					indicator = Atc.SignalIndicators.Green;
				}
				// --- suffix ---
				if (code.EndsWith("ORP")) {
					code = code.Substring(0, code.Length - 3);
					overrunProtector = true;
				}
				// --- code ---
				Atc.Signal signal = null;
				if (code.Contains("/")) {
					int separator = code.IndexOf('/');
					string a = code.Substring(0, separator);
					string b = code.Substring(separator + 1);
					if (b.Contains("@")) {
						separator = b.IndexOf('@');
						string c = b.Substring(separator + 1);
						b = b.Substring(0, separator);
						double an, bn, cn;
						if (double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out an) && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out bn) && double.TryParse(c, NumberStyles.Float, CultureInfo.InvariantCulture, out cn)) {
							if (an < 0.0) return null;
							if (bn < 0.0) return null;
							if (cn < 0.0) return null;
							if (an < bn) return null;
							signal = new Atc.Signal(aspect, indicator, (double)an / 3.6, (double)bn / 3.6, cn);
						}
					} else {
						double an, bn;
						if (double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out an) && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out bn)) {
							if (an < 0.0) return null;
							if (bn < 0.0) return null;
							if (an < bn) return null;
							signal = new Atc.Signal(aspect, indicator, (double)an / 3.6, (double)bn / 3.6, double.MaxValue);
						}
					}
				} else if (code.Contains("@")) {
					int separator = code.IndexOf('@');
					string b = code.Substring(0, separator);
					string c = code.Substring(separator + 1);
					double bn, cn;
					if (double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out bn) && double.TryParse(c, NumberStyles.Float, CultureInfo.InvariantCulture, out cn)) {
						if (bn < 0.0) return null;
						if (cn < 0.0) return null;
						signal = new Atc.Signal(aspect, indicator, double.MaxValue, (double)bn / 3.6, cn);
					}
				} else {
					int value;
					if (int.TryParse(code, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) {
						if (value < 0.0) return null;
						signal = new Atc.Signal(aspect, indicator, (double)value / 3.6);
					}
				}
				if (signal == null) {
					return null;
				}
				signal.Kirikae = kirikae ? Atc.KirikaeStates.ToAts : Atc.KirikaeStates.ToAtc;
				signal.ZenpouYokoku = zenpouYokoku;
				signal.OverrunProtector = overrunProtector;
				return signal;
			}
		}
		
		/// <summary>Sets up the devices from the specified train.dat file.</summary>
		/// <param name="file">The train.dat file.</param>
		internal void LoadTrainDatFile(string file) {
			string[] lines = File.ReadAllLines(file, Encoding.UTF8);
			for (int i = 0; i < lines.Length; i++) {
				int semicolon = lines[i].IndexOf(';');
				if (semicolon >= 0) {
					lines[i] = lines[i].Substring(0, semicolon).Trim();
				} else {
					lines[i] = lines[i].Trim();
				}
			}
			for (int i = 0; i < lines.Length; i++) {
				if (lines[i].Equals("#DEVICE", StringComparison.OrdinalIgnoreCase)) {
					if (i < lines.Length - 1) {
						int value = int.Parse(lines[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture);
						if (value == 0) {
							this.AtsSx = new AtsSx(this);
						} else if (value == 1) {
							this.AtsSx = new AtsSx(this);
							this.AtsP = new AtsP(this);
						}
					}
					if (i < lines.Length - 2) {
						int value = int.Parse(lines[i + 2], NumberStyles.Integer, CultureInfo.InvariantCulture);
						if (value == 1) {
							this.Atc = new Atc(this);
						} else if (value == 2) {
							this.Atc = new Atc(this);
							this.Atc.AutomaticSwitch = true;
						}
					}
					if (i < lines.Length - 3) {
						int value = int.Parse(lines[i + 3], NumberStyles.Integer, CultureInfo.InvariantCulture);
						if (value == 1) {
							this.Eb = new Eb(this);
						}
					}
					break;
				}
			}
			// --- devices ---
			List<Device> devices = new List<Device>();
			if (this.Eb != null) {
				devices.Add(this.Eb);
			}
			if (this.Atc != null) {
				devices.Add(this.Atc);
			}
			if (this.AtsP != null) {
				devices.Add(this.AtsP);
			}
			if (this.AtsSx != null) {
				devices.Add(this.AtsSx);
			}
			this.Devices = devices.ToArray();
		}

		/// <summary>Is called when the system should initialize.</summary>
		/// <param name="mode">The initialization mode.</param>
		internal void Initialize(InitializationModes mode) {
			this.PluginInitializing = true;
			for (int i = this.Devices.Length - 1; i >= 0; i--) {
				this.Devices[i].Initialize(mode);
			}
		}
		
		/// <summary>Is called every frame.</summary>
		/// <param name="data">The data.</param>
		internal void Elapse(ElapseData data) {
			this.PluginInitializing = false;
			if (data.ElapsedTime.Seconds > 0.0 & data.ElapsedTime.Seconds < 1.0) {
				// --- panel ---
				for (int i = 0; i < this.Panel.Length; i++) {
					this.Panel[i] = 0;
				}
				// --- devices ---
				this.State = data.Vehicle;
				this.Handles = new ReadOnlyHandles(data.Handles);
				bool blocking = false;
				foreach (var device in this.Devices) {
					device.Elapse(data, ref blocking);
				}
				if (data.Handles.BrakeNotch != 0) {
					data.Handles.PowerNotch = 0;
				}
				// --- panel ---
				this.Panel[255] = 1;
				int seconds = (int)Math.Floor(data.TotalTime.Seconds);
				this.Panel[10] = (seconds / 3600) % 24;
				this.Panel[11] = (seconds / 60) % 60;
				this.Panel[12] = seconds % 60;
				this.Panel[269] = data.Handles.ConstSpeed ? 1 : 0;
				if (data.Handles.Reverser != 0 & (this.Handles.PowerNotch > 0 & this.Handles.BrakeNotch == 0 | this.Handles.PowerNotch == 0 & this.Handles.BrakeNotch == 1 & this.Specs.HasHoldBrake)) {
					this.Panel[100] = 1;
				}
				if (data.Handles.BrakeNotch >= this.Specs.AtsNotch & data.Handles.BrakeNotch <= this.Specs.BrakeNotches | data.Handles.Reverser != 0 & data.Handles.BrakeNotch == 1 & this.Specs.HasHoldBrake) {
					this.Panel[101] = 1;
				}
				for (int i = (int)VirtualKeys.S; i <= (int)VirtualKeys.C2; i++) {
					if (KeysPressed[i]) {
						this.Panel[93 + i] = 1;
					}
				}
				if (PanelIllumination) {
					this.Panel[161] = 1;
				}
				// --- accelerometer ---
				this.AccelerometerTimer += data.ElapsedTime.Seconds;
				if (this.AccelerometerTimer > AccelerometerMaximumTimer) {
					this.Acceleration = (data.Vehicle.Speed.MetersPerSecond - AccelerometerSpeed) / this.AccelerometerTimer;
					this.AccelerometerSpeed = data.Vehicle.Speed.MetersPerSecond;
					this.AccelerometerTimer = 0.0;
				}
				if (this.Acceleration < 0.0) {
					double value = -3.6 * this.Acceleration;
					if (value >= 10.0) {
						this.Panel[74] = 9;
						this.Panel[75] = 9;
					} else {
						this.Panel[74] = (int)Math.Floor(value) % 10;
						this.Panel[75] = (int)Math.Floor(10.0 * value) % 10;
					}
				}
				// --- sound ---
				this.Sounds.Elapse(data);
				// --- AI ---
				this.Calling.Elapse(data);
			}
		}
		
		/// <summary>Is called when the driver changes the reverser.</summary>
		/// <param name="reverser">The new reverser position.</param>
		internal void SetReverser(int reverser) {
			foreach (var device in this.Devices) {
				device.SetReverser(reverser);
			}
		}
		
		/// <summary>Is called when the driver changes the power notch.</summary>
		/// <param name="powerNotch">The new power notch.</param>
		internal void SetPower(int powerNotch) {
			foreach (var device in this.Devices) {
				device.SetPower(powerNotch);
			}
		}
		
		/// <summary>Is called when the driver changes the brake notch.</summary>
		/// <param name="brakeNotch">The new brake notch.</param>
		internal void SetBrake(int brakeNotch) {
			foreach (var device in this.Devices) {
				device.SetBrake(brakeNotch);
			}
		}
		
		/// <summary>Is called when a key is pressed.</summary>
		/// <param name="key">The key.</param>
		internal void KeyDown(VirtualKeys key) {
			int index = (int)key;
			if (index >= 0 & index < KeysPressed.Length) {
				KeysPressed[index] = true;
			}
			foreach (var device in this.Devices) {
				device.KeyDown(key);
			}
			if (key == VirtualKeys.L) {
				this.PanelIllumination = !this.PanelIllumination;
			}
		}
		
		/// <summary>Is called when a key is released.</summary>
		/// <param name="key">The key.</param>
		internal void KeyUp(VirtualKeys key) {
			int index = (int)key;
			if (index >= 0 & index < KeysPressed.Length) {
				KeysPressed[index] = false;
			}
			foreach (var device in this.Devices) {
				device.KeyUp(key);
			}
		}
		
		/// <summary>Is called when a horn is played or when the music horn is stopped.</summary>
		/// <param name="type">The type of horn.</param>
		internal void HornBlow(HornTypes type) {
			foreach (var device in this.Devices) {
				device.HornBlow(type);
			}
		}
		
		/// <summary>Is called when the state of the doors changes.</summary>
		/// <param name="oldState">The old state of the doors.</param>
		/// <param name="newState">The new state of the doors.</param>
		public void DoorChange(DoorStates oldState, DoorStates newState) {
			this.Doors = newState;
			foreach (var device in this.Devices) {
				device.DoorChange(oldState, newState);
			}
			this.Calling.DoorChange(oldState, newState);
		}
		
		/// <summary>Is called to inform about signals.</summary>
		/// <param name="signal">The signal data.</param>
		internal void SetSignal(SignalData[] signal) {
			this.KnownSignals = new Signal[signal.Length];
			for (int i = 0; i < signal.Length; i++) {
				this.KnownSignals[i] = new Signal(this.State.Location + signal[i].Distance, signal[i].Aspect);
			}
			if (this.ContinuousAnalogTransmissionAvailable) {
				for (int i = 0; i < signal.Length; i++) {
					double position = this.State.Location + signal[i].Distance;
					if (Math.Abs(position - this.ContinuousAnalogTransmissionSignalLocation) < 5.0) {
						int signalAspect = signal[i].Aspect;
						if (signalAspect >= 10) {
							signalAspect = 0;
						}
						int frequency;
						if (signalAspect == this.ContinuousAnalogTransmissionSignalAspect) {
							frequency = this.ContinuousAnalogTransmissionActiveFrequency;
						} else {
							frequency = this.ContinuousAnalogTransmissionIdleFrequency;
						}
						if (frequency != this.ContinuousAnalogTransmissionLastFrequency) {
							this.ContinuousAnalogTransmissionLastFrequency = frequency;
							int beaconType = this.ContinuousAnalogTransmissionActiveFrequency;
							int beaconOptional = 1000 * this.ContinuousAnalogTransmissionSignalAspect + this.ContinuousAnalogTransmissionIdleFrequency;
							this.SetBeacon(new BeaconData(beaconType, beaconOptional, signal[i]));
						}
						break;
					}
				}
			}
			foreach (var device in this.Devices) {
				device.SetSignal(signal);
			}
			this.Calling.SetSignal(signal);
		}
		
		/// <summary>Is called when a beacon is passed.</summary>
		/// <param name="beacon">The beacon data.</param>
		internal void SetBeacon(BeaconData beacon) {
			// --- adjust signal aspect ---
			const double tolerance = 5.0;
			double location = this.State.Location + beacon.Signal.Distance;
			for (int i = 0; i < this.KnownSignals.Length; i++) {
				if (this.KnownSignals[i].Location < location - tolerance) {
					if (this.KnownSignals[i].Aspect <= 0 | this.KnownSignals[i].Aspect >= 10) {
						beacon = new BeaconData(beacon.Type, beacon.Optional, new SignalData(0, beacon.Signal.Distance));
						break;
					}
				}
			}
			// --- process beacon ---
			if (beacon.Type == 44) {
				/*
				 * Frequency-based continuous analog transmission encodes
				 * the frequencies in KHz in the optional data:
				 * 
				 * siiiaaann
				 * |\_/\_/\/
				 * | |  |  \- status (00 = not available, 01 = available)
				 * | |  \---- active frequency (KHz)
				 * | \------- idle frequency (KHz)
				 * \--------- signal aspect
				 */
				int status = beacon.Optional % 100;
				if (status == 0) {
					this.ContinuousAnalogTransmissionAvailable = false;
					this.ContinuousAnalogTransmissionLastFrequency = 0;
				} else if (status == 1) {
					int activeFrequency = (beacon.Optional / 100) % 1000;
					if (activeFrequency >= 73) {
						int idleFrequency = (beacon.Optional / 100000) % 1000;
						int signalAspect = beacon.Optional / 100000000;
						if (signalAspect >= 10) signalAspect = 0;
						if (!this.ContinuousAnalogTransmissionAvailable) {
							int beaconType = activeFrequency;
							int beaconOptional = 1000 * signalAspect + idleFrequency;
							this.SetBeacon(new BeaconData(beaconType, beaconOptional, beacon.Signal));
						}
						this.ContinuousAnalogTransmissionAvailable = true;
						this.ContinuousAnalogTransmissionSignalLocation = this.State.Location + beacon.Signal.Distance;
						this.ContinuousAnalogTransmissionSignalAspect = signalAspect;
						this.ContinuousAnalogTransmissionActiveFrequency = activeFrequency;
						this.ContinuousAnalogTransmissionIdleFrequency = idleFrequency;
					}
				}
			} else {
				foreach (var device in this.Devices) {
					device.SetBeacon(beacon);
				}
				this.Calling.SetBeacon(beacon);
			}
		}
		
		
		// --- static functions ---
		
		/// <summary>Gets the frequency a beacon is transmitting at, or 0 if not recognized.</summary>
		/// <param name="beacon">The beacon.</param>
		/// <returns>The frequency the beacon is transmitting at, or 0 if not recognized.</returns>
		internal static int GetFrequencyFromBeacon(BeaconData beacon) {
			/*
			 * Frequency-based beacons encode the frequency as the
			 * beacon type in KHz and have the following optional data:
			 * 
			 * siii
			 * |\_/
			 * | \- idle frequency (KHz)
			 * \--- signal aspect
			 * 
			 * or
			 * 
			 * -1
			 * always active
			 * 
			 * If the aspect of the signal the beacon is attached to
			 * matches the aspect encoded in the optional data, the
			 * beacon transmits at its active frequency. Otherwise,
			 * the beacon transmits at its idle frequency.
			 * 
			 * If the optional data is -1, the beacon always transmits
			 * at its active frequency.
			 * */
			if (beacon.Type >= 73) {
				if (beacon.Optional == -1) {
					return beacon.Type;
				} else {
					int beaconAspect = beacon.Optional / 1000;
					if (beaconAspect >= 10) beaconAspect = 0;
					int signalAspect = beacon.Signal.Aspect;
					if (signalAspect >= 10) signalAspect = 0;
					if (beaconAspect == signalAspect) {
						return beacon.Type;
					} else {
						int idle = beacon.Optional % 1000;
						return idle;
					}
				}
			} else {
				return 0;
			}
		}
		
	}
}
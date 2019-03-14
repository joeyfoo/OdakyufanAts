﻿using System;
using OpenBveApi.Runtime;

namespace Plugin {
	internal class AI {
		
		// --- members ---
		
		/// <summary>The underlying train.</summary>
		private Train Train;
		
		
		// --- constructors ---
		
		/// <summary>Creates a new AI.</summary>
		/// <param name="train">The underlying train.</param>
		internal AI(Train train) {
			this.Train = train;
		}
		
		
		// --- functions ---
		
		/// <summary>Is called when the plugin should perform the AI.</summary>
		/// <param name="data">The AI data.</param>
		internal void Perform(AIData data) {
			// --- ats-sx ---
			if (this.Train.AtsSx != null) {
				if (this.Train.AtsSx.State == AtsSx.States.Disabled) {
					this.Train.KeyDown(VirtualKeys.D);
					data.Response = AIResponse.Long;
					return;
				} else if (this.Train.AtsSx.State == AtsSx.States.Chime) {
					bool cancel = false;
					if (this.Train.State.Location > this.Train.AtsSx.RedSignalLocation) {
						cancel = true;
					} else if (this.Train.Calling.SignalType != 0 && this.Train.Calling.SignalAspect != 0) {
						double threshold = Math.Abs(this.Train.Calling.SignalLocation - this.Train.AtsSx.RedSignalLocation);
						if (threshold < 50.0) {
							cancel = true;
						}
					}
					if (cancel) {
						this.Train.KeyDown(VirtualKeys.A1);
						data.Response = AIResponse.Medium;
						return;
					}
				} else if (this.Train.AtsSx.State == AtsSx.States.Alarm) {
					if (data.Handles.PowerNotch > 0) {
						data.Handles.PowerNotch--;
						data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
						return;
					} else if (data.Handles.BrakeNotch < this.Train.Specs.AtsNotch) {
						data.Handles.BrakeNotch++;
						data.Response = data.Handles.BrakeNotch < this.Train.Specs.AtsNotch ? AIResponse.Short : AIResponse.Medium;
						return;
					} else {
						this.Train.KeyDown(VirtualKeys.S);
						data.Response = AIResponse.Medium;
						return;
					}
				} else if (this.Train.AtsSx.State == AtsSx.States.Emergency) {
					if (data.Handles.PowerNotch > 0) {
						data.Handles.PowerNotch--;
						data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
						return;
					} else if (data.Handles.BrakeNotch <= this.Train.Specs.BrakeNotches) {
						data.Handles.BrakeNotch++;
						data.Response = data.Handles.BrakeNotch <= this.Train.Specs.BrakeNotches ? AIResponse.Short : AIResponse.Medium;
						return;
					} else if (data.Handles.Reverser != 0) {
						data.Handles.Reverser = 0;
						data.Response = AIResponse.Medium;
						return;
					} else if (Math.Abs(this.Train.State.Speed.KilometersPerHour) < 1.0) {
						this.Train.KeyDown(VirtualKeys.B1);
						data.Response = AIResponse.Long;
						return;
					} else {
						data.Response = AIResponse.Long;
						return;
					}
				}
			}
			// --- ats-ps ---
			if (this.Train.AtsPs != null) {
				if (this.Train.AtsPs.State == AtsPs.States.Disabled) {
					this.Train.KeyDown(VirtualKeys.F);
					data.Response = AIResponse.Long;
					return;
				} else if (this.Train.AtsPs.State == AtsPs.States.Pattern | this.Train.AtsPs.State == AtsPs.States.Approaching) {
					double limit = double.MaxValue;
					foreach (AtsPs.Pattern pattern in this.Train.AtsPs.Patterns) {
						if (pattern.Distance != double.MaxValue) {
							if (pattern.SpeedPattern < limit) {
								limit = pattern.SpeedPattern;
							}
						}
					}
					if (limit != double.MaxValue) {
						double a, b, c, d;
						if (limit < 30.0 / 3.6) {
							a = 0.67 * limit;
							b = 0.50 * limit;
							c = 0.33 * limit;
						} else {
							a = limit - 10.0 / 3.6;
							b = limit - 15.0 / 3.6;
							c = limit - 20.0 / 3.6;
						}
						d = limit - 25.0 / 3.6;
						if (this.Train.State.Speed.MetersPerSecond >= a) {
							// --- full service brakes ---
							if (data.Handles.PowerNotch > 0) {
								data.Handles.PowerNotch--;
								data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
								return;
							} else if (data.Handles.BrakeNotch < this.Train.Specs.BrakeNotches) {
								data.Handles.BrakeNotch++;
								data.Response = data.Handles.BrakeNotch < this.Train.Specs.BrakeNotches ? AIResponse.Short : AIResponse.Long;
								return;
							} else {
								data.Response = AIResponse.Long;
								return;
							}
						} else if (this.Train.State.Speed.MetersPerSecond >= b) {
							// --- B67 notch ---
							if (data.Handles.PowerNotch > 0) {
								data.Handles.PowerNotch--;
								data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
								return;
							} else if (data.Handles.BrakeNotch < this.Train.Specs.B67Notch) {
								data.Handles.BrakeNotch++;
								data.Response = data.Handles.BrakeNotch < this.Train.Specs.B67Notch ? AIResponse.Short : AIResponse.Long;
								return;
							}
						} else if (this.Train.State.Speed.MetersPerSecond >= c) {
							// --- ATS cancel notch ---
							if (data.Handles.PowerNotch > 0) {
								data.Handles.PowerNotch--;
								data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
								return;
							} else if (data.Handles.BrakeNotch <= this.Train.Specs.AtsNotch) {
								data.Handles.BrakeNotch++;
								data.Response = data.Handles.BrakeNotch <= this.Train.Specs.AtsNotch ? AIResponse.Short : AIResponse.Long;
								return;
							}
						} else if (this.Train.State.Speed.MetersPerSecond >= d) {
							// --- cruise ---
							if (data.Handles.PowerNotch > 1) {
								data.Handles.PowerNotch--;
								data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Long;
								return;
							}
						}
					}
				} else if (this.Train.AtsPs.State == AtsPs.States.Emergency) {
					if (data.Handles.PowerNotch > 0) {
						data.Handles.PowerNotch--;
						data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
						return;
					} else if (data.Handles.BrakeNotch <= this.Train.Specs.BrakeNotches) {
						data.Handles.BrakeNotch++;
						data.Response = data.Handles.BrakeNotch <= this.Train.Specs.BrakeNotches ? AIResponse.Short : AIResponse.Medium;
						return;
					} else if (data.Handles.Reverser != 0) {
						data.Handles.Reverser = 0;
						data.Response = AIResponse.Medium;
						return;
					} else if (Math.Abs(this.Train.State.Speed.KilometersPerHour) < 1.0) {
						this.Train.KeyDown(VirtualKeys.B1);
						data.Response = AIResponse.Long;
						return;
					} else {
						data.Response = AIResponse.Long;
						return;
					}
				}
			}
			// --- ats-p ---
			if (this.Train.AtsP != null) {
				if (this.Train.AtsP.State == AtsP.States.Disabled) {
					this.Train.KeyDown(VirtualKeys.E);
					data.Response = AIResponse.Long;
					return;
				} else if (this.Train.AtsP.State == AtsP.States.Pattern) {
					if (this.Train.State.Speed.MetersPerSecond > 15.0 / 3.6) {
						if (data.Handles.PowerNotch > 0) {
							data.Handles.PowerNotch--;
							data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
							return;
						} else if (data.Handles.BrakeNotch <= this.Train.Specs.AtsNotch) {
							data.Handles.BrakeNotch++;
							data.Response = data.Handles.BrakeNotch <= this.Train.Specs.AtsNotch ? AIResponse.Short : AIResponse.Long;
							return;
						}
					}
				} else if (this.Train.AtsP.State == AtsP.States.Brake) {
					if (data.Handles.PowerNotch > 0) {
						data.Handles.PowerNotch--;
						data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
						return;
					} else if (Math.Abs(this.Train.State.Speed.MetersPerSecond) < 1.0 / 3.6) {
						if (data.Handles.BrakeNotch < this.Train.Specs.BrakeNotches) {
							data.Handles.BrakeNotch++;
							data.Response = data.Handles.BrakeNotch < this.Train.Specs.BrakeNotches ? AIResponse.Short : AIResponse.Medium;
							return;
						} else if (data.Handles.Reverser != 0) {
							data.Handles.Reverser = 0;
							data.Response = AIResponse.Medium;
							return;
						} else {
							this.Train.KeyDown(VirtualKeys.B1);
							data.Response = AIResponse.Long;
							return;
						}
					}
				} else if (this.Train.AtsP.State == AtsP.States.Service | this.Train.AtsP.State == AtsP.States.Emergency) {
					if (data.Handles.PowerNotch > 0) {
						data.Handles.PowerNotch--;
						data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
						return;
					} else if (data.Handles.BrakeNotch < this.Train.Specs.BrakeNotches) {
						data.Handles.BrakeNotch++;
						data.Response = data.Handles.BrakeNotch < this.Train.Specs.BrakeNotches ? AIResponse.Short : AIResponse.Medium;
						return;
					} else if (data.Handles.Reverser != 0) {
						data.Handles.Reverser = 0;
						data.Response = AIResponse.Medium;
						return;
					} else if (Math.Abs(this.Train.State.Speed.KilometersPerHour) < 1.0) {
						this.Train.KeyDown(VirtualKeys.B1);
						data.Response = AIResponse.Long;
						return;
					} else {
						data.Response = AIResponse.Long;
						return;
					}
				}
			}
			// --- atc ---
			if (this.Train.Atc != null) {
				if (this.Train.Atc.State == Atc.States.Disabled) {
					this.Train.KeyDown(VirtualKeys.G);
					data.Response = AIResponse.Long;
					return;
				} else if (this.Train.Atc.EmergencyOperation) {
					this.Train.KeyDown(VirtualKeys.H);
					data.Response = AIResponse.Long;
					return;
				} else if (this.Train.Atc.ShouldSwitchToAts()) {
					this.Train.KeyDown(VirtualKeys.C1);
					data.Response = AIResponse.Long;
					return;
				} else if (this.Train.Atc.ShouldSwitchToAtc()) {
					this.Train.KeyDown(VirtualKeys.C2);
					data.Response = AIResponse.Long;
					return;
				} else if (this.Train.Atc.State == Atc.States.Normal | this.Train.Atc.State == Atc.States.ServiceHalf | this.Train.Atc.State == Atc.States.ServiceFull) {
					if (this.Train.State.Speed.KilometersPerHour > 15.0) {
						if (this.Train.State.Speed.MetersPerSecond > this.Train.Atc.Pattern.CurrentSpeed - 5.0 / 3.6) {
							if (data.Handles.PowerNotch > 0) {
								data.Handles.PowerNotch--;
								data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
								return;
							} else if (data.Handles.BrakeNotch <= this.Train.Specs.AtsNotch) {
								data.Handles.BrakeNotch++;
								data.Response = data.Handles.BrakeNotch <= this.Train.Specs.AtsNotch ? AIResponse.Short : AIResponse.Long;
								return;
							}
						} else if (this.Train.State.Speed.MetersPerSecond > this.Train.Atc.Pattern.CurrentSpeed - 10.0 / 3.6) {
							if (data.Handles.PowerNotch > 0) {
								data.Handles.PowerNotch--;
								data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Long;
								return;
							}
						}
						if (this.Train.Atc.Pattern.CurrentSpeed == 0.0) {
							data.Response = AIResponse.Long;
						}
					}
				} else if (this.Train.Atc.State == Atc.States.Emergency) {
					if (data.Handles.PowerNotch > 0) {
						data.Handles.PowerNotch--;
						data.Response = data.Handles.PowerNotch > 0 ? AIResponse.Short : AIResponse.Medium;
						return;
					} else if (data.Handles.BrakeNotch < this.Train.Specs.B67Notch) {
						data.Handles.BrakeNotch++;
						data.Response = data.Handles.BrakeNotch < this.Train.Specs.B67Notch ? AIResponse.Short : AIResponse.Medium;
						return;
					} else {
						data.Response = AIResponse.Long;
						return;
					}
				}
			}
			// --- eb ---
			if (this.Train.Eb != null) {
				if (this.Train.Eb.Counter >= this.Train.Eb.TimeUntilBell) {
					this.Train.KeyDown(VirtualKeys.A2);
					data.Response = AIResponse.Long;
				}
			}
		}
		
	}
}
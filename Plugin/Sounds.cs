using System;
using OpenBveApi.Runtime;

namespace Plugin {
	internal class Sounds {
		
		// --- classes ---
		
		/// <summary>Represents a sound.</summary>
		internal class Sound {
			internal int Index;
			internal SoundHandle Handle;
			internal bool IsToBePlayed;
			internal Sound(int index) {
				this.Index = index;
				this.Handle = null;
			}
			internal void Play() {
				this.IsToBePlayed = true;
			}
		}
		
		
		// --- members ---
		
		private PlaySoundDelegate PlaySound;
		
		
		// --- looping sounds ---
		
		internal Sound AtsBell;

		internal Sound AtsChime;
		
		internal Sound ToAtsReminder;
		
		internal Sound ToAtcReminder;
		
		internal Sound Eb;

		private Sound[] LoopingSounds;
		
		
		// --- play once sounds ---
		
		internal Sound AtsPBell;
		
		internal Sound AtsPsPatternEstablishment;
		
		internal Sound AtsPsPatternRelease;
		
		internal Sound AtsPsChime;
		
		internal Sound AtcBell;
		
		internal Sound ToAts;
		
		internal Sound ToAtc;
		
		//internal Sound AtcAspectUp;
		
		//internal Sound AtcAspectDown;
		
		private Sound[] PlayOnceSounds;
		
		
		// --- constructors ---
		
		/// <summary>Creates a new instance of sounds.</summary>
		/// <param name="playSound">The delegate to the function to play sounds.</param>
		internal Sounds(PlaySoundDelegate playSound) {
			this.PlaySound = playSound;
			// --- looping ---
			this.AtsBell = new Sound(0);
			this.AtsChime = new Sound(1);
			this.ToAtsReminder = new Sound(10);
			this.ToAtcReminder = new Sound(11);
			this.Eb = new Sounds.Sound(13);
			this.LoopingSounds = new Sound[] { this.AtsBell, this.AtsChime, this.ToAtsReminder, this.ToAtcReminder, this.Eb };
			// --- play once ---
			this.AtsPBell = new Sound(2);
			this.AtsPsPatternEstablishment = new Sound(3);
			this.AtsPsPatternRelease = new Sound(4);
			this.AtsPsChime = new Sound(5);
			this.AtcBell = new Sound(7);
			this.ToAts = new Sound(8);
			this.ToAtc = new Sound(9);
			this.PlayOnceSounds = new Sound[] { this.AtsPBell, this.AtsPsPatternEstablishment, this.AtsPsPatternRelease, this.AtsPsChime, this.AtcBell, this.ToAts, this.ToAtc };
		}

		
		// --- functions ---
		
		/// <summary>Is called every frame.</summary>
		/// <param name="data">The data.</param>
		internal void Elapse(ElapseData data) {
			foreach (Sound sound in this.LoopingSounds) {
				if (sound.IsToBePlayed) {
					if (sound.Handle == null || sound.Handle.Stopped) {
						sound.Handle = PlaySound(sound.Index, 1.0, 1.0, true);
					}
				} else {
					if (sound.Handle != null && sound.Handle.Playing) {
						sound.Handle.Stop();
					}
				}
				sound.IsToBePlayed = false;
			}
			foreach (Sound sound in this.PlayOnceSounds) {
				if (sound.IsToBePlayed) {
					PlaySound(sound.Index, 1.0, 1.0, false);
					sound.IsToBePlayed = false;
				}
			}
		}
		
	}
}
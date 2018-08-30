using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using api;

namespace api.Devices {
    public class Hold: Device {
        private int _length = 200; // milliseconds
        private Queue<Timer> _timers = new Queue<Timer>();
        private TimerCallback _timerexit;

        public int Length {
            get {
                return _length;
            }
            set {
                if (0 <= value)
                    _length = value;
            }
        }

        public override Device Clone() {
            return new Hold(_length);
        }

        public Hold() {
            _timerexit = new TimerCallback(Tick);
        }

        public Hold(int length) {
            _timerexit = new TimerCallback(Tick);
            Length = length;
        }

        public Hold(Action<Signal> exit) {
            _timerexit = new TimerCallback(Tick);
            MIDIExit = exit;
        }

        public Hold(int length, Action<Signal> exit) {
            _timerexit = new TimerCallback(Tick);
            Length = length;
            MIDIExit = exit;
        }

        private void Tick(object info) {
            if (info.GetType() == typeof(byte)) {
                Signal n = new Signal((byte)info, new Color(0));
      
                if (MIDIExit != null)
                    MIDIExit(n);
                
                _timers.Dequeue();
            }
        }

        public override void MIDIEnter(Signal n) {
            if (n.Color.Lit) {
                _timers.Enqueue(new Timer(_timerexit, n.Index, _length, System.Threading.Timeout.Infinite));
                
                if (MIDIExit != null)
                    MIDIExit(n);
            }
        }
    }
}
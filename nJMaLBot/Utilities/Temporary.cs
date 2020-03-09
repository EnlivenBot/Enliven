using System;

namespace Bot.Utilities {
    public class Temporary<T> {
        private readonly Func<T> _factory;
        private readonly TimeSpan _lifetime;
        private readonly object _valueLock = new object();

        private T _value;
        private bool _hasValue;
        private DateTime _creationTime;

        public Temporary(Func<T> factory, TimeSpan lifetime) {
            this._factory = factory;
            this._lifetime = lifetime;
        }

        public T Value {
            get {
                var now = DateTime.Now;
                lock (this._valueLock) {
                    if (this._hasValue) {
                        if (this._creationTime.Add(this._lifetime) < now) {
                            this._hasValue = false;
                        }
                    }

                    if (this._hasValue) return this._value;
                    this._value = this._factory();
                    this._hasValue = true;
                        
                    this._creationTime = DateTime.Now;

                    return this._value;
                }
            }
        }
    }
}
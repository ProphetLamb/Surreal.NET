using System.Collections;

namespace SurrealDB.Models.Result;

public readonly partial record struct DriverResponse {
    public struct Enumerator : IEnumerator<RawResult> {
        private readonly Result.DriverResponse _rsp;
        private int _pos;
        private RawResult _current;

        internal Enumerator(Result.DriverResponse rsp) {
            _rsp = rsp;
        }

        public readonly RawResult Current => _current;

        object IEnumerator.Current => Current;

        public bool Next(out RawResult current) {
            int pos = _pos;
            if (pos < 0) {
                current = default;
                return false;
            }

            if (pos == 0 && _rsp.IsSingle) {
                current = _rsp._single;
                _pos = pos + 1;
                return true;
            }

            if (pos < _rsp.Results.Length) {
                current = _rsp.Results[pos];
                _pos = pos + 1;
                return true;
            }

            current = default;
            return false;
        }

        public bool MoveNext() {
            return Next(out _current);
        }

        public void Reset() {
            _pos = 0;
        }

        public void Dispose() {
            _pos = -1;
        }
    }

    public struct ErrorIterator : IEnumerator<ErrorResult>, IEnumerable<ErrorResult> {
        private Enumerator _en;
        private ErrorResult _cur;

        internal ErrorIterator(Enumerator en) {
            _en = en;
        }

        public bool MoveNext() {
            while (_en.MoveNext()) {
                if (!_en.Current.TryGetAnyError(out ErrorResult err)) {
                    continue;
                }

                _cur = err;
                return true;
            }

            _cur = default;
            return false;
        }

        public void Reset() {
            _en.Reset();
        }

        public readonly ErrorResult Current => _cur;

        object IEnumerator.Current => _en.Current;

        public void Dispose() {
            _en.Dispose();
        }

        public ErrorIterator GetEnumerator() {
            return this; // copy
        }

        IEnumerator<ErrorResult> IEnumerable<ErrorResult>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    public struct OkIterator : IEnumerator<OkResult>, IEnumerable<OkResult> {
        private Enumerator _en;
        private OkResult _cur;

        internal OkIterator(Enumerator en) {
            _en = en;
        }

        public bool MoveNext() {
            while (_en.MoveNext()) {
                if (_en.Current.TryGetOk(out OkResult ok)) {
                    _cur = ok;
                    return true;
                }
            }

            _cur = default;
            return false;
        }

        public void Reset() {
            _en.Reset();
        }

        public OkResult Current => _cur;

        object IEnumerator.Current => _en.Current;

        public void Dispose() {
            _en.Dispose();
        }

        public OkIterator GetEnumerator() {
            return this; // copy
        }

        IEnumerator<OkResult> IEnumerable<OkResult>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<RawResult> IEnumerable<RawResult>.GetEnumerator() {
        return GetEnumerator();
    }
}

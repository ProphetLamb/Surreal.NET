using System.Collections;
using System.Diagnostics;

namespace SurrealDB.Common;

public sealed class TaskList {
    private readonly object _lock = new();
    private readonly Node _root;
    private Node _tail;
    private int _len;

    public TaskList() {
        _root = _tail = new(Task.CompletedTask);
    }

    public void Add(Task task) {
        lock (_lock) {
            Debug.Assert(_tail.Next is null);
            Node tail = new(task) { Prev = _tail };
            _tail.Next = tail;
            _tail = tail;
            _len += 1;
        }
    }

    public void Trim() {
        lock (_lock) {
            Node? pos = _root;
            do {
                Node cur = pos;
                pos = pos.Next;
                Task task = cur.Task;
                if (task.IsCompleted) {
                    Remove(cur);
                }
            } while (pos is not null);
        }
    }

    public ValueTask WhenAll() {
        return _len == 0 ? default : new(Task.WhenAll(Drain()));
    }

    public DrainIterator Drain() {
        return new DrainIterator(this);
    }

    /// <summary>
    /// Removes the node from the list. Requires _lock!
    /// </summary>
    private bool Remove(Node node) {
        if (Object.ReferenceEquals(_root, node)) {
            // Do not remove the root!
            return false;
        }
        Node? prev = node.Prev;
        Node? next = node.Next;
        if (Object.ReferenceEquals(_tail, node)) {
            _tail = prev!; // cannot be null because of _root
        }
        if (prev is not null) {
            prev.Next = next;
        }
        if (next is not null) {
            next.Prev = prev;
        }

        _len -= 1;
        return true;
    }

    private sealed class Node {
        public readonly Task Task;
        public Node? Next;
        public Node? Prev;

        public Node(Task task) {
            Task = task;
        }
    }

    public struct DrainIterator : IEnumerable<Task>, IEnumerator<Task> {
        private readonly TaskList _list;

        public DrainIterator(TaskList list) {
            _list = list;
        }

        public DrainIterator GetEnumerator() {
            return new(_list);
        }

        IEnumerator<Task> IEnumerable<Task>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public bool MoveNext() {
            lock (_list._lock) {
                return _list.Remove(_list._tail);
            }
        }

        public void Reset() {
            throw new NotSupportedException("Cannot be reset");
        }

        public Task Current => _list._tail.Task;

        object IEnumerator.Current => Current;

        public void Dispose() {
            // not needed
        }
    }
}

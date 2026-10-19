using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Stakeholders.App.UI
{
    /// <summary>A row created once per key and re-bound on every refresh.</summary>
    public class ListRow<T>
    {
        public VisualElement Root;
        public T Item;
        public Action<T> Bind;
    }

    /// <summary>
    /// Reconciles a container's children against a list of items by key. Rows persist between
    /// refreshes, so buttons keep working while the game ticks and scroll positions stay put.
    /// </summary>
    public class KeyedList<T>
    {
        readonly VisualElement _container;
        readonly Func<T, string> _key;
        readonly Func<ListRow<T>> _create;
        readonly Dictionary<string, ListRow<T>> _rows = new Dictionary<string, ListRow<T>>();
        readonly Label _empty;

        public KeyedList(VisualElement container, Func<T, string> key, Func<ListRow<T>> create, string emptyText = null)
        {
            _container = container;
            _key = key;
            _create = create;
            if (!string.IsNullOrEmpty(emptyText))
            {
                _empty = Ui.Text(emptyText, "dim", "small");
                _empty.style.paddingTop = 8;
                _empty.style.paddingBottom = 8;
            }
        }

        public int Count => _rows.Count;

        public void Sync(IList<T> items)
        {
            var seen = new HashSet<string>();
            int index = 0;
            foreach (var item in items)
            {
                string k = _key(item);
                if (k == null || !seen.Add(k)) continue;
                if (!_rows.TryGetValue(k, out var row))
                {
                    row = _create();
                    _rows[k] = row;
                }
                row.Item = item;
                row.Bind?.Invoke(item);
                if (index >= _container.childCount || _container[index] != row.Root)
                {
                    row.Root.RemoveFromHierarchy();
                    _container.Insert(Math.Min(index, _container.childCount), row.Root);
                }
                index++;
            }
            var remove = new List<string>();
            foreach (var kv in _rows) if (!seen.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (var k in remove)
            {
                _rows[k].Root.RemoveFromHierarchy();
                _rows.Remove(k);
            }
            if (_empty != null)
            {
                if (_rows.Count == 0 && _empty.parent == null) _container.Add(_empty);
                else if (_rows.Count > 0 && _empty.parent != null) _empty.RemoveFromHierarchy();
            }
        }
    }
}

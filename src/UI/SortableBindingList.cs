using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    // BindingList that DataGridView can sort by clicking a column header.
    // Text sorts in natural order (P2 before P10), ignoring case.
    public sealed class SortableBindingList<T>:BindingList<T>
    {
        bool sorted;ListSortDirection direction;PropertyDescriptor property;
        public SortableBindingList(IEnumerable<T> items):base(items.ToList()){}
        protected override bool SupportsSortingCore{get{return true;}}
        protected override bool IsSortedCore{get{return sorted;}}
        protected override ListSortDirection SortDirectionCore{get{return direction;}}
        protected override PropertyDescriptor SortPropertyCore{get{return property;}}
        protected override void ApplySortCore(PropertyDescriptor prop,ListSortDirection dir)
        {
            var list=(List<T>)Items;
            // Stable: equal keys keep their current (e.g. Excel row) order.
            var ordered=list.Select((item,index)=>new{item,index}).OrderBy(x=>prop.GetValue(x.item),Comparer<object>.Create(RoomSearch.Compare)).ThenBy(x=>x.index).Select(x=>x.item).ToList();
            if(dir==ListSortDirection.Descending)
                ordered=list.Select((item,index)=>new{item,index}).OrderByDescending(x=>prop.GetValue(x.item),Comparer<object>.Create(RoomSearch.Compare)).ThenBy(x=>x.index).Select(x=>x.item).ToList();
            list.Clear();list.AddRange(ordered);
            sorted=true;direction=dir;property=prop;
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset,-1));
        }
        protected override void RemoveSortCore(){sorted=false;property=null;}
    }

    public static class GridSort
    {
        // Binds rows and keeps the column the user sorted by when the list is refreshed.
        public static void Bind<T>(DataGridView grid,IEnumerable<T> rows)
        {
            var column=grid.SortedColumn;var order=grid.SortOrder;
            var list=new SortableBindingList<T>(rows);
            grid.DataSource=list;
            foreach(DataGridViewColumn c in grid.Columns)if(c.SortMode==DataGridViewColumnSortMode.NotSortable)c.SortMode=DataGridViewColumnSortMode.Automatic;
            if(column!=null&&order!=SortOrder.None&&grid.Columns.Contains(column))
                grid.Sort(column,order==SortOrder.Descending?ListSortDirection.Descending:ListSortDirection.Ascending);
        }
    }
}

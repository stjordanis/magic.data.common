﻿/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Text;
using magic.node;
using magic.node.extensions;
using magic.data.common.helpers;

namespace magic.data.common
{
    /// <summary>
    /// Specialised select SQL builder, to create a select SQL statement by semantically traversing an input node.
    /// </summary>
    public class SqlReadBuilder : SqlWhereBuilder
    {
        /// <summary>
        /// Creates a select SQL statement
        /// </summary>
        /// <param name="node">Root node to generate your SQL from.</param>
        /// <param name="escapeChar">Escape character to use for escaping table names etc.</param>
        public SqlReadBuilder(Node node, string escapeChar)
            : base(node, escapeChar)
        { }

        /// <summary>
        /// Builds your select SQL statement, and returns a structured SQL statement, plus any parameters.
        /// </summary>
        /// <returns>Node containing insert SQL as root node, and parameters as children.</returns>
        public override Node Build()
        {
            // Return value.
            var result = new Node("sql");

            // Starting build process.
            var builder = new StringBuilder();
            builder.Append("select ");
            AppendColumns(builder);
            builder.Append(" from ");
            AppendTableName(builder);
            AppendWhere(builder, result);
            AppendTail(builder);

            // Returning result to caller.
            result.Value = builder.ToString();
            return result;
        }

        #region [ -- Protected, overridden, and virtual methods -- ]

        /// <inheritdoc />
        protected override void AppendTableName(StringBuilder builder)
        {
            var tableNode = Root.Children.FirstOrDefault(x => x.Name == "table") ??
                throw new ArgumentException($"No [table] argument supplied to {GetType().FullName}");

            builder.Append(EscapeTypeName(tableNode.GetEx<string>()));

            // Then making sure we apply [join] tables, if there are any.
            foreach (var idxJoin in tableNode.Children.Where(x => x.Name == "join"))
            {
                AppendJoinedTables(builder, idxJoin);
            }
        }

        /// <summary>
        /// Adds limit and offset parts to your SQL if requested by caller.
        /// </summary>
        /// <param name="builder">Where to put the resulting SQL into.</param>
        protected virtual void AppendTail(StringBuilder builder)
        {
            // Order counts!
            AppendGroupBy(builder);
            AppendOrderBy(builder);
            AppendLimit(builder);
            AppendOffset(builder);
        }

        /// <summary>
        /// Appends the order by clause into builder.
        /// </summary>
        /// <param name="builder">Builder where clause should be appended.</param>
        protected virtual void AppendOrderBy(StringBuilder builder)
        {
            var orderNodes = Root.Children.Where(x => x.Name == "order");
            if (orderNodes.Any())
            {
                // Sanity checking.
                if (orderNodes.Count() > 1)
                    throw new ArgumentException($"syntax error in '{GetType().FullName}', too many [order] nodes");

                var orderValue = string.Join(
                    ",",
                    orderNodes.First()
                        .GetEx<string>()
                        .Split(',')
                        .Select(x => EscapeTypeName(x.Trim())));
                builder.Append(" order by " + orderValue);
                AppendDirection(builder);
            }
            else
            {
                /*
                 * Some databases requires "default order by" statement, such as
                 * for instance MS SQL Server does in cases where we have defined a
                 * "limit" and "offset".
                 */
                AppendDefaultOrderBy(builder);
            }
        }

        /// <summary>
        /// Adds the default order by clause for queries, in cases where no explicit
        /// order by was added. Some databse vendors, such as MS SQL requires this
        /// given some specific conditions.
        /// </summary>
        /// <param name="builder">Where to put the default order by clause.</param>
        protected virtual void AppendDefaultOrderBy(StringBuilder builder)
        { }

        #endregion

        #region [ -- Private helper methods -- ]

        /*
         * Appends all requested columns into resulting builder.
         */
        void AppendColumns(StringBuilder builder)
        {
            var columnsNodes = Root.Children.Where(x => x.Name == "columns");
            if (!columnsNodes.Any())
            {
                // Caller did not explicitly declare columns, hence defaulting to all.
                builder.Append("*");
                return;
            }

            // Sanity checking invocation.
            if (columnsNodes.Count() > 1)
                throw new ArgumentException("You can only declare [columns] once in your lambda.");

            // Adding all columns caller requested to SQL.
            builder.Append(string.Join(",", columnsNodes
                .First()
                .Children
                .Select(x => GetSingleColumn(x))));
        }

        /*
         * Appends a single column name into resulting builder.
         */
        string GetSingleColumn(Node column)
        {
            var builder = new StringBuilder();
            if (column.Name.Contains("(") && column.Name.Contains(")"))
            {
                builder.Append(column.Name); // Aggregate column, avoid escaping.
            }
            else
            {
                // Checking if column name is escaped.
                if (column.Name.StartsWith("\\"))
                    builder.Append(EscapeColumnName(column.Name.Substring(1)));
                else
                    builder.Append(EscapeTypeName(column.Name));
            }

            // Checking if caller supplied an alias for column.
            var alias = column.Children.FirstOrDefault(x => x.Name == "as")?.GetEx<string>();
            if (!string.IsNullOrEmpty(alias))
                builder.Append(" as ").Append(EscapeColumnName(alias));

            return builder.ToString();
        }

        /*
         * Appends joined tables into builder.
         */
        void AppendJoinedTables(
            StringBuilder builder,
            Node joinNode)
        {
            // Appending join and its type, making sure we sanity check invocation first.
            var joinType = joinNode.Children
                .FirstOrDefault(x => x.Name == "type")?
                .GetEx<string>() ??
                "inner";
            switch (joinType)
            {
                case "left":
                case "right":
                case "inner":
                case "full":
                    builder.Append(" ")
                        .Append(joinType)
                        .Append(" join ");
                    break;
                default:
                    throw new ArgumentException($"I don't understand '{joinType}' here, only [left], [right], [inner] and [full]");
            }

            // Appending secondary table name, and its "on" parts.
            builder.Append(EscapeTypeName(joinNode.GetEx<string>())).Append(" on ");

            // Retrieving and appending all "on" criteria.
            var onNode = joinNode.Children.FirstOrDefault(x => x.Name == "on") ??
                throw new ArgumentException("No [on] argument supplied to [join]");
            AppendBooleanLevel(onNode, null, builder);

            // Recursively iterating through all nested joins.
            foreach (var idxInner in joinNode.Children.Where(x => x.Name == "join"))
            {
                AppendJoinedTables(builder, idxInner);
            }
        }

        /*
         * Appends any [group] (by) arguments, if given.
         */
        void AppendGroupBy(StringBuilder builder)
        {
            // Checking if we have a [group] argument.
            var groupByNodes = Root.Children.Where(x => x.Name == "group");
            if (!groupByNodes.Any())
                return;

            // Sanity checking that we only have one [group] argument.
            if (groupByNodes.Count() > 1)
                throw new ArgumentException("I can only handle one [group] argument.");

            // Appending group by stamenent into builder.
            builder.Append(" group by ");

            var groupByNode = groupByNodes.First();
            builder.Append(string.Join(",", groupByNode.Children.Select(x => EscapeTypeName(x.Name))));
        }

        /*
         * Appends direction for order operation, asc/desc, if it exists.
         */
        void AppendDirection(StringBuilder builder)
        {
            // Checking if [direction] node exists.
            var direction = Root.Children.Where(x => x.Name == "direction");
            if (direction.Any())
            {
                // Sanity checking.
                if (direction.Count() > 1)
                    throw new ArgumentException($"syntax error in '{GetType().FullName}', too many [direction] nodes");

                var dir = direction.First().GetEx<string>();
                switch (dir)
                {
                    case "asc":
                    case "desc":
                        builder.Append(" ").Append(dir);
                        break;
                    default:
                        throw new ArgumentException($"I don't know how to sort according to the '{dir}' [direction], only 'asc' and 'desc'");
                }
            }
        }

        /*
         * Appends limit parts, if existing.
         */
        void AppendLimit(StringBuilder builder)
        {
            var limitNodes = Root.Children.Where(x => x.Name == "limit");
            if (limitNodes.Any())
            {
                // Sanity checking.
                if (limitNodes.Count() > 1)
                    throw new ArgumentException($"syntax error in '{GetType().FullName}', too many [limit] nodes");

                var limitValue = limitNodes.First().GetEx<long>();
                if (limitValue > -1)
                    builder.Append(" limit " + limitValue);
            }
            else
            {
                // Defaulting to 25 records, unless [limit] was explicitly given.
                builder.Append(" limit 25");
            }
        }

        /*
         * Appends offset parts, if existing.
         */
        void AppendOffset(StringBuilder builder)
        {
            var offsetNodes = Root.Children.Where(x => x.Name == "offset");
            if (offsetNodes.Any())
            {
                // Sanity checking.
                if (offsetNodes.Count() > 1)
                    throw new ArgumentException($"syntax error in '{GetType().FullName}', too many [offset] nodes");

                var offsetValue = offsetNodes.First().GetEx<long>();
                builder.Append(" offset " + offsetValue);
            }
        }

        #endregion
    }
}

﻿/*
 * Magic, Copyright(c) Thomas Hansen 2019 - thomas@gaiasoul.com
 * Licensed as Affero GPL unless an explicitly proprietary license has been obtained.
 */

using System;
using System.Linq;
using System.Text;
using magic.node;
using magic.node.extensions;

namespace magic.data.common
{
    /// <summary>
    /// Specialised select SQL builder, to create a select SQL statement by semantically traversing an input node.
    /// </summary>
    public abstract class SqlReadBuilder : SqlBuilder
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

            // Getting columns.
            GetColumns(builder);

            builder.Append(" from ");

            // Getting table name from base class.
            GetTableName(builder);

            // Getting [where] clause.
            BuildWhere(result, builder);

            // Adding tail.
            GetTail(builder);

            // Returning result to caller.
            result.Value = builder.ToString();
            return result;
        }

        #region [ -- Protected and virtual methods -- ]

        /// <summary>
        /// Adds limit and offset parts to your SQL if requested by caller.
        /// </summary>
        /// <param name="builder">Where to put the resulting SQL into.</param>
        protected virtual void GetTail(StringBuilder builder)
        {
            // Getting [order].
            GetOrderBy(builder);

            // Getting [limit].
            var limitNodes = Root.Children.Where(x => x.Name == "limit");
            if (limitNodes.Any())
            {
                // Sanity checking.
                if (limitNodes.Count() > 1)
                    throw new ApplicationException($"syntax error in '{GetType().FullName}', too many [limit] nodes");

                var limitValue = limitNodes.First().GetEx<long>();
                builder.Append(" limit " + limitValue);
            }
            else
            {
                // Defaulting to 25 records, unless [limit] was explicitly given.
                builder.Append(" limit 25");
            }

            var offsetNodes = Root.Children.Where(x => x.Name == "offset");
            if (offsetNodes.Any())
            {
                // Sanity checking.
                if (offsetNodes.Count() > 1)
                    throw new ApplicationException($"syntax error in '{GetType().FullName}', too many [offset] nodes");

                var offsetValue = offsetNodes.First().GetEx<long>();
                builder.Append(" offset " + offsetValue);
            }
        }

        /// <summary>
        /// Appends the order by clause into builder.
        /// </summary>
        /// <param name="builder">Builder where clause should be appended.</param>
        protected virtual void GetOrderBy(StringBuilder builder)
        {
            var orderNodes = Root.Children.Where(x => x.Name == "order");
            if (orderNodes.Any())
            {
                // Sanity checking.
                if (orderNodes.Count() > 1)
                    throw new ApplicationException($"syntax error in '{GetType().FullName}', too many [order] nodes");

                var orderColumn = orderNodes.First().GetEx<string>().Replace(EscapeChar, EscapeChar + EscapeChar);
                builder.Append(" order by " + EscapeChar + orderColumn + EscapeChar);

                // Checking if [direction] node exists.
                var direction = Root.Children.Where(x => x.Name == "direction");
                if (direction.Any())
                {
                    // Sanity checking.
                    if (direction.Count() > 1)
                        throw new ApplicationException($"syntax error in '{GetType().FullName}', too many [direction] nodes");

                    var dir = direction.First().GetEx<string>();
                    if (dir != "asc" && dir != "desc")
                        throw new ArgumentException($"I don't know how to sort according to the '{dir}' [direction], only 'asc' and 'desc'");

                    builder.Append(" " + dir);
                }
            }
        }

        #endregion

        #region [ -- Private helper methods -- ]

        void GetColumns(StringBuilder builder)
        {
            var columns = Root.Children.Where(x => x.Name == "columns");
            if (columns.Any() && columns.First().Children.Any())
            {
                var first = true;
                foreach (var idx in columns.First().Children)
                {
                    if (first)
                        first = false;
                    else
                        builder.Append(",");

                    builder.Append(EscapeChar + idx.Name.Replace(EscapeChar, EscapeChar + EscapeChar) + EscapeChar);
                }
            }
            else
            {
                builder.Append("*");
            }
        }

        #endregion
    }
}

/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using Xunit;
using magic.node;
using magic.node.extensions;

namespace magic.data.common.tests.tests.read
{
    public class OrderByReadTests
    {
        [Fact]
        public void OrderBySingleField()
        {
            // Creating node hierarchy.
            var node = new Node();
            node.Add(new Node("table", "foo"));
            node.Add(new Node("order", "fieldOrder"));
            var builder = new SqlReadBuilder(node, "'");

            // Extracting SQL + params, and asserting correctness.
            var result = builder.Build();
            var sql = result.Get<string>();
            Assert.Equal("select * from 'foo' order by 'fieldOrder' limit 25", sql);
        }

        [Fact]
        public void OrderByWithTableName()
        {
            // Creating node hierarchy.
            var node = new Node();
            node.Add(new Node("table", "foo"));
            node.Add(new Node("order", "foo.fieldOrder"));
            var builder = new SqlReadBuilder(node, "'");

            // Extracting SQL + params, and asserting correctness.
            var result = builder.Build();
            var sql = result.Get<string>();
            Assert.Equal("select * from 'foo' order by 'foo'.'fieldOrder' limit 25", sql);
        }

        [Fact]
        public void OrderByMultipleFields()
        {
            // Creating node hierarchy.
            var node = new Node();
            node.Add(new Node("table", "foo"));
            node.Add(new Node("order", "foo.fieldOrder1, foo.fieldOrder2"));
            var builder = new SqlReadBuilder(node, "'");

            // Extracting SQL + params, and asserting correctness.
            var result = builder.Build();
            var sql = result.Get<string>();
            Assert.Equal("select * from 'foo' order by 'foo'.'fieldOrder1','foo'.'fieldOrder2' limit 25", sql);
        }

        [Fact]
        public void MultipleOrderBy_Throws()
        {
            // Creating node hierarchy.
            var node = new Node();
            node.Add(new Node("table", "foo"));
            node.Add(new Node("order", "fieldOrder1"));
            node.Add(new Node("order", "fieldOrder2"));
            var builder = new SqlReadBuilder(node, "'");

            // Extracting SQL + params, and asserting correctness.
            Assert.Throws<ArgumentException>(() => builder.Build());
        }

        [Fact]
        public void MultipleDirections_Throws()
        {
            // Creating node hierarchy.
            var node = new Node();
            node.Add(new Node("table", "foo"));
            node.Add(new Node("order", "fieldOrder"));
            node.Add(new Node("direction", "desc"));
            node.Add(new Node("direction", "desc"));
            var builder = new SqlReadBuilder(node, "'");

            // Extracting SQL + params, and asserting correctness.
            Assert.Throws<ArgumentException>(() => builder.Build());
        }

        [Fact]
        public void BadDirection_Throws()
        {
            // Creating node hierarchy.
            var node = new Node();
            node.Add(new Node("table", "foo"));
            node.Add(new Node("order", "fieldOrder"));
            node.Add(new Node("direction", "throws"));
            var builder = new SqlReadBuilder(node, "'");

            // Extracting SQL + params, and asserting correctness.
            Assert.Throws<ArgumentException>(() => builder.Build());
        }

        [Fact]
        public void OrderByDescending()
        {
            // Creating node hierarchy.
            var node = new Node();
            node.Add(new Node("table", "foo"));
            node.Add(new Node("order", "fieldOrder"));
            node.Add(new Node("direction", "desc"));
            var builder = new SqlReadBuilder(node, "'");

            // Extracting SQL + params, and asserting correctness.
            var result = builder.Build();
            var sql = result.Get<string>();
            Assert.Equal("select * from 'foo' order by 'fieldOrder' desc limit 25", sql);
        }
    }
}

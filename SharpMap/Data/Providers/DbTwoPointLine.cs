﻿// Copyright 2013 - Lothar Otto (www.sodako.de)
//
// This file is part of SharpMap.
// SharpMap is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// SharpMap is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with SharpMap; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA 

using NetTopologySuite.Geometries;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;

namespace SharpMap.Data.Providers
{
    /// <summary>
    /// The DbTwoPointLine provider is used for rendering line data from an ADO.NET compatible data source.
    /// </summary>
    /// <remarks>
    /// <para>The data source will need to have two double-type columns, xColumn and yColumn that contains the coordinates of the point,
    /// and an integer-type column containing a unique identifier for each row.</para>
    /// <para>To get good performance, make sure you have applied indexes on ID, xColumn and yColumns in your data source table.</para>
    /// </remarks>
    [Serializable]
    public class DbTwoPointLine : PreparedGeometryProvider
    {
        private string _definitionQuery;

        /// <summary>
        /// Initializes a new instance of the DbTwoPointLine provider
        /// </summary>
        /// <param name="provider">The ADO.NET database provider factory</param>
        /// <param name="connectionString">The connection string</param>
        /// <param name="tableName">The name of the table</param>
        /// <param name="oidColumnName">The name of the object id column</param>
        /// <param name="xColumnBegin">The name of the x-ordinates column of the beginning of the line</param>
        /// <param name="yColumnBegin">The name of the y-ordinates column of the beginning of the line</param>
        /// <param name="xColumnEnd">The name of the x-ordinates column of the end of the line</param>
        /// <param name="yColumnEnd">The name of the y-ordinates column of the end of the line</param>
        public DbTwoPointLine(DbProviderFactory provider, string connectionString, string tableName, string oidColumnName, string xColumnBegin, string yColumnBegin, string xColumnEnd, string yColumnEnd)
        {
            Table = tableName;
            XColumnBegin = xColumnBegin;
            YColumnBegin = yColumnBegin;
            XColumnEnd = xColumnEnd;
            YColumnEnd = yColumnEnd;
            ObjectIdColumn = oidColumnName;
            DbProvider = provider;
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Data table name
        /// </summary>
        public string Table { get; set; }


        /// <summary>
        /// Name of column that contains the Object ID
        /// </summary>
        public string ObjectIdColumn { get; set; }

        /// <summary>
        /// Name of column that contains X coordinate start
        /// </summary>
        public string XColumnBegin { get; set; }

        /// <summary>
        /// Name of column that contains Y coordinate start
        /// </summary>
        public string YColumnBegin { get; set; }

        /// <summary>
        /// Name of column that contains X coordinate start
        /// </summary>
        public string XColumnEnd { get; set; }

        /// <summary>
        /// Name of column that contains Y coordinate start
        /// </summary>
        public string YColumnEnd { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the connection string
        /// </summary>
        public string ConnectionString
        {
            get { return ConnectionID; }
            set { ConnectionID = value; }
        }

        /// <summary>
        /// Gets or sets an entity decorator.
        /// </summary>
        /// <remarks>
        /// For Access this would e.g. be &quot;[{0}]&quot;, for SQLite, Postgres or SqlServer &quot;\&quot;{0}\&quot;&quot;
        /// </remarks>
        public string EntityDecorator { get; set; } = "{0}";

        /// <summary>
        /// The <see cref="DbProviderFactory"/> used to create connections, commands etc.
        /// </summary>
        private DbProviderFactory DbProvider { get; }

        /// <summary>
        /// Definition query used for limiting data set
        /// </summary>
        public string DefinitionQuery
        {
            get { return _definitionQuery; }
            set { _definitionQuery = value; }
        }

        /// <summary>
        /// Returns geometries within the specified bounding box
        /// </summary>
        /// <param name="bbox"></param>
        /// <returns></returns>
        public override Collection<Geometry> GetGeometriesInView(Envelope bbox)
        {
            var features = new Collection<Geometry>();
            using (var conn = DbProvider.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;
                //open the connection
                conn.Open();

                var strSQL = "SELECT " + XColumnBegin + ", " + YColumnBegin + ", " + XColumnEnd + ", " + YColumnEnd + " FROM " + Table + " WHERE ";
                strSQL += GetDefinitionQueryConstraint(true);
                //Limit to the points within the bounding box
                strSQL += GetSpatialConstraint(bbox);

                var factory = Factory;
                var precisionModel = factory.PrecisionModel;

                using (var command = conn.CreateCommand())
                {
                    command.CommandText = strSQL;

                    using (var dr = command.ExecuteReader())
                    {
                        if (dr != null && dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                if (!(dr.IsDBNull(0) || dr.IsDBNull(1)))
                                {
                                    var c1 = new Coordinate(Convert.ToDouble(dr[0]), Convert.ToDouble(dr[1]));
                                    precisionModel.MakePrecise(c1);
                                    var c2 = new Coordinate(Convert.ToDouble(dr[2]), Convert.ToDouble(dr[3]));
                                    precisionModel.MakePrecise(c2);
                                    features.Add(Factory.CreateLineString(new[] { c1, c2 }));
                                }
                            }
                        }
                    }
                }
            }
            return features;
        }

        /// <summary>
        /// Returns geometry Object IDs whose bounding box intersects 'bbox'
        /// </summary>
        /// <param name="bbox"></param>
        /// <returns></returns>
        public override Collection<uint> GetObjectIDsInView(Envelope bbox)
        {
            var objectlist = new Collection<uint>();
            using (var conn = DbProvider.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;
                //open the connection
                conn.Open();

                var strSQL = "SELECT " + ObjectIdColumn + " FROM " + Table + " WHERE ";

                //Limit to the DefinitionQuery
                strSQL += GetDefinitionQueryConstraint(true);

                //Limit to the points within the boundingbox
                strSQL += GetSpatialConstraint(bbox);

                using (var command = conn.CreateCommand())
                {
                    command.CommandText = strSQL;

                    using (var dr = command.ExecuteReader())
                    {
                        if (dr != null && dr.HasRows)
                        {
                            while (dr.Read())
                                if (!dr.IsDBNull(0))
                                    objectlist.Add((uint)dr.GetInt32(0));
                        }
                    }
                    conn.Close();
                }
            }
            return objectlist;
        }

        /// <summary>
        /// Returns the geometry corresponding to the Object ID
        /// </summary>
        /// <param name="oid">Object ID</param>
        /// <returns>geometry</returns>
        public override Geometry GetGeometryByID(uint oid)
        {
            using (var conn = DbProvider.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;

                conn.Open();

                var strSQL = "SELECT " + XColumnBegin + ", " + YColumnBegin + ", " + XColumnEnd + ", " + YColumnEnd + " FROM " + Table + " WHERE " + ObjectIdColumn +
                                "=" + oid.ToString(Map.NumberFormatEnUs);

                using (var command = conn.CreateCommand())
                {
                    command.CommandText = strSQL;

                    using (var dr = command.ExecuteReader())
                    {
                        if (dr != null && dr.HasRows)
                        {
                            if (dr.Read())
                            {
                                //If the read row is OK, create a point geometry from the XColumn and YColumn and return it
                                if (!(dr.IsDBNull(0) || dr.IsDBNull(1)))
                                {

                                    var c1 = new Coordinate(Convert.ToDouble(dr[0]), Convert.ToDouble(dr[1]));
                                    Factory.PrecisionModel.MakePrecise(c1);
                                    var c2 = new Coordinate(Convert.ToDouble(dr[2]), Convert.ToDouble(dr[3]));
                                    Factory.PrecisionModel.MakePrecise(c2);
                                    return Factory.CreateLineString(new[] { c1, c2 });

                                }
                            }
                        }
                    }
                    conn.Close();
                }
            }
            return null;
        }

        /// <summary>
        /// Function to limit the points to the <paramref name="bbox"/>.
        /// </summary>
        /// <param name="bbox">The spatial predicate bounding box</param>
        /// <returns>A SQL string limiting the result set based on an Envelope constraint.</returns>
        private string GetSpatialConstraint(Envelope bbox)
        {
            return string.Format(Map.NumberFormatEnUs, "NOT ({0} > {1} OR {2} < {3} OR {4} > {5} OR {6} < {7})",
                bbox.MinX, GetMaxSql(ToEntity(XColumnBegin), ToEntity(XColumnEnd)),
                bbox.MaxX, GetMinSql(ToEntity(XColumnBegin), ToEntity(XColumnEnd)),
                bbox.MinY, GetMaxSql(ToEntity(YColumnBegin), ToEntity(YColumnEnd)),
                bbox.MaxY, GetMinSql(ToEntity(YColumnBegin), ToEntity(YColumnEnd)));
        }

        /// <summary>
        /// Function to build a Max function to return the maximum value of two column entities
        /// </summary>
        /// <param name="entity1">The first column entity</param>
        /// <param name="entity2">The second column entity</param>
        /// <returns>An SQL CASE string to mimic <see cref="System.Math.Max(double, double)"/></returns>
        private string GetMaxSql(string entity1, string entity2)
        {
            return $"(CASE WHEN {entity1} >= {entity2} THEN {entity1} ELSE {entity2} END)";
        }

        /// <summary>
        /// Function to build a Min function to return the maximum value of two column entities
        /// </summary>
        /// <param name="entity1">The first column entity</param>
        /// <param name="entity2">The second column entity</param>
        /// <returns>An SQL CASE string to mimic <see cref="System.Math.Min(double, double)"/></returns>
        private string GetMinSql(string entity1, string entity2)
        {
            return $"(CASE WHEN {entity1} <= {entity2} THEN {entity1} ELSE {entity2} END)";
        }

        /// <summary>
        /// Function to properly decorate a database entity e.g. (table-, query- or column name)
        /// </summary>
        /// <param name="name">The name or the database entity</param>
        /// <returns>The decorated entity</returns>
        private string ToEntity(string name)
        {
            return string.Format(EntityDecorator, name);
        }

        /// <summary>
        /// Function to limit the features based on <see cref="DefinitionQuery"/>
        /// </summary>
        /// <param name="addAnd">Defines if " AND " should be appended.</param>
        /// <returns>A SQL string limiting the resultset, if desired.</returns>
        private string GetDefinitionQueryConstraint(bool addAnd)
        {
            var addAndText = addAnd ? " AND" : string.Empty;
            if (!string.IsNullOrEmpty(_definitionQuery))
                return string.Format("{0}{1} ", _definitionQuery, addAndText);
            return string.Empty;
        }

        /// <summary>
        /// Returns all features with the view box
        /// </summary>
        /// <param name="bbox">view box</param>
        /// <param name="ds">FeatureDataSet to fill data into</param>
        public override void ExecuteIntersectionQuery(Envelope bbox, FeatureDataSet ds)
        {
            //List<Geometries.Geometry> features = new List<SharpMap.Geometries.Geometry>();
            using (var conn = DbProvider.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;

                conn.Open();

                var strSQL = "SELECT * FROM " + Table + " WHERE ";
                //If a definition query has been specified, add this as a filter on the query
                strSQL += GetDefinitionQueryConstraint(true);

                //Limit to the points within the boundingbox
                strSQL += GetSpatialConstraint(bbox);

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = strSQL;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader == null)
                            throw new InvalidOperationException();

                        //Set up result table
                        var fdt = new FeatureDataTable();
                        fdt.TableName = Table;
                        for (var c = 0; c < reader.FieldCount; c++)
                        {
                            var fieldType = reader.GetFieldType(c);
                            if (fieldType == null)
                                throw new Exception("Failed to retrieve field type for column: " + c);
                            fdt.Columns.Add(reader.GetName(c), fieldType);
                        }

                        var dataTransfer = new object[reader.FieldCount];

                        //Get factory and precision model
                        var factory = Factory;
                        var pm = factory.PrecisionModel;

                        fdt.BeginLoadData();
                        while (reader.Read())
                        {
                            var count = reader.GetValues(dataTransfer);
                            System.Diagnostics.Debug.Assert(count == dataTransfer.Length);

                            var fdr = (FeatureDataRow)fdt.LoadDataRow(dataTransfer, true);
                            var c1 = new Coordinate(Convert.ToDouble(fdr[XColumnBegin]), Convert.ToDouble(fdr[YColumnBegin]));
                            pm.MakePrecise(c1);
                            var c2 = new Coordinate(Convert.ToDouble(fdr[XColumnEnd]), Convert.ToDouble(fdr[YColumnEnd]));
                            pm.MakePrecise(c2);
                            fdr.Geometry = Factory.CreateLineString(new[] { c1, c2 });
                        }
                        fdt.EndLoadData();

                        ds.Tables.Add(fdt);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the number of features in the dataset
        /// </summary>
        /// <returns>Total number of features</returns>
        public override int GetFeatureCount()
        {
            using (var conn = DbProvider.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;

                conn.Open();

                var strSQL = "SELECT Count(*) FROM " + Table;
                if (!String.IsNullOrEmpty(_definitionQuery))
                    //If a definition query has been specified, add this as a filter on the query
                    strSQL += " WHERE " + _definitionQuery;

                using (var command = conn.CreateCommand())
                {
                    command.CommandText = strSQL;
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// Returns a datarow based on a RowID
        /// </summary>
        /// <param name="rowId"></param>
        /// <returns>datarow</returns>
        public override FeatureDataRow GetFeature(uint rowId)
        {
            using (var conn = DbProvider.CreateConnection())
            {
                var strSQL = "SELECT * FROM " + Table + " WHERE " + ObjectIdColumn + "=" + rowId.ToString(Map.NumberFormatEnUs);

                conn.ConnectionString = ConnectionString;
                conn.Open();

                using (var selectCommand = conn.CreateCommand())
                {
                    selectCommand.CommandText = strSQL;

                    using (var adapter = DbProvider.CreateDataAdapter())
                    {
                        adapter.SelectCommand = selectCommand;

                        var ds = new DataSet();
                        adapter.Fill(ds);
                        conn.Close();
                        if (ds.Tables.Count > 0)
                        {
                            var fdt = new FeatureDataTable(ds.Tables[0]);
                            foreach (DataColumn col in ds.Tables[0].Columns)
                                fdt.Columns.Add(col.ColumnName, col.DataType, col.Expression);
                            if (ds.Tables[0].Rows.Count > 0)
                            {
                                var dr = ds.Tables[0].Rows[0];
                                var fdr = fdt.NewRow();
                                foreach (DataColumn col in ds.Tables[0].Columns)
                                    fdr[col.Ordinal] = dr[col];
                                if (dr[XColumnBegin] != DBNull.Value && dr[YColumnBegin] != DBNull.Value && dr[XColumnEnd] != DBNull.Value && dr[YColumnEnd] != DBNull.Value)
                                    fdr.Geometry = Factory.CreateLineString(new[] { new Coordinate((double)dr[XColumnBegin], (double)dr[YColumnBegin]), new Coordinate((double)dr[XColumnEnd], (double)dr[YColumnEnd]) });

                                return fdr;
                            }
                            return null;
                        }
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Function to return the <see cref="Envelope"/> of dataset
        /// </summary>
        /// <returns>The extent of the dataset</returns>
        public override Envelope GetExtents()
        {
            using (var conn = DbProvider.CreateConnection())
            {
                conn.ConnectionString = ConnectionString;

                conn.Open();
                var strSQL = "SELECT Min(" + XColumnBegin + ") as MinXBegin, Min(" + YColumnBegin + ") As MinYBegin, " +
                                    "Max(" + XColumnBegin + ") As MaxXBegin, Max(" + YColumnBegin + ") As MaxYBegin, " +
                                    "Min(" + XColumnEnd + ") as MinXEnd, Min(" + YColumnEnd + ") As MinYEnd, " +
                                    "Max(" + XColumnEnd + ") As MaxXEnd, Max(" + YColumnEnd + ") As MaxYEnd " +
                                    " FROM " + Table;

                //If a definition query has been specified, add this as a filter on the query
                if (!String.IsNullOrEmpty(_definitionQuery))
                    strSQL += " WHERE " + _definitionQuery;

                using (var command = conn.CreateCommand())
                {
                    command.CommandText = strSQL;

                    using (var dr = command.ExecuteReader())
                    {
                        if (dr != null && dr.HasRows)
                            if (dr.Read())
                            {
                                //If the read row is OK, create a point geometry from the XColumn and YColumn and return it
                                if (dr[0] != DBNull.Value && dr[1] != DBNull.Value && dr[2] != DBNull.Value && dr[3] != DBNull.Value
                                     && dr[4] != DBNull.Value && dr[5] != DBNull.Value && dr[6] != DBNull.Value && dr[7] != DBNull.Value)
                                {
                                    //Find min/max from begin/end of the line
                                    double minX;
                                    if (Convert.ToDouble(dr["MinXBegin"]) < Convert.ToDouble(dr["MinXEnd"]))
                                        minX = Convert.ToDouble(dr["MinXBegin"]);
                                    else
                                        minX = Convert.ToDouble(dr["MinXEnd"]);

                                    double minY;
                                    if (Convert.ToDouble(dr["MinYBegin"]) < Convert.ToDouble(dr["MinYEnd"]))
                                        minY = Convert.ToDouble(dr["MinYBegin"]);
                                    else
                                        minY = Convert.ToDouble(dr["MinYEnd"]);

                                    double maxX;
                                    if (Convert.ToDouble(dr["MaxXBegin"]) > Convert.ToDouble(dr["MaxXEnd"]))
                                        maxX = Convert.ToDouble(dr["MaxXBegin"]);
                                    else
                                        maxX = Convert.ToDouble(dr["MaxXEnd"]);

                                    double maxY;
                                    if (Convert.ToDouble(dr["MaxYBegin"]) > Convert.ToDouble(dr["MaxYEnd"]))
                                        maxY = Convert.ToDouble(dr["MaxYBegin"]);
                                    else
                                        maxY = Convert.ToDouble(dr["MaxYEnd"]);

                                    return new Envelope(new Coordinate(minX, minY),
                                                        new Coordinate(maxX, maxY));
                                }
                            }
                    }
                    conn.Close();
                }
            }
            return null;
        }

        #region Disposers and finalizers

        #endregion
    }
}

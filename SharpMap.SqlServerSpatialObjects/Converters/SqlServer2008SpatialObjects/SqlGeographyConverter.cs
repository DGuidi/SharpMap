﻿using Microsoft.SqlServer.Types;
using NetTopologySuite.Geometries;
using SharpMap.Data.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Factory = NetTopologySuite.Geometries.GeometryFactory;
using SMGeometry = NetTopologySuite.Geometries.Geometry;
using SMGeometryCollection = NetTopologySuite.Geometries.GeometryCollection;
using SMGeometryType = NetTopologySuite.Geometries.OgcGeometryType;
using SMLinearRing = NetTopologySuite.Geometries.LinearRing;
using SMLineString = NetTopologySuite.Geometries.LineString;
using SMMultiLineString = NetTopologySuite.Geometries.MultiLineString;
using SMMultiPoint = NetTopologySuite.Geometries.MultiPoint;
using SMMultiPolygon = NetTopologySuite.Geometries.MultiPolygon;
using SMPoint = NetTopologySuite.Geometries.Point;
using SMPolygon = NetTopologySuite.Geometries.Polygon;

namespace SharpMap.Converters.SqlServer2008SpatialObjects
{
    /// <summary>
    /// Exception for failing conversions of SqlServer geographies
    /// </summary>
    [Serializable]
    public class SqlGeographyConverterException : Exception
    {
        /// <summary>
        /// The geometry to convert
        /// </summary>
        public readonly SMGeometry Geometry;

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        public SqlGeographyConverterException()
        { }

        /// <summary>
        /// Creates an instance of this class providing the failing <paramref name="geometry"/>.
        /// </summary>
        /// <param name="geometry">A geometry</param>
        public SqlGeographyConverterException(SMGeometry geometry)
            : this("Failed to convert SharpMapGeometry", geometry)
        {
            Geometry = geometry;
        }

        /// <summary>
        /// Creates an instance of this class providing an <paramref name="inner"/> exception
        /// and the failing <paramref name="geometry"/>.
        /// </summary>
        /// <param name="inner">An inner exception</param>
        /// <param name="geometry">A geometry</param>
        public SqlGeographyConverterException(Exception inner, SMGeometry geometry)
            : this("Failed to convert SharpMapGeometry", inner, geometry)
        {
        }

        /// <summary>
        /// Creates an instance of this class providing a <paramref name="message"/> and the failing <paramref name="geometry"/>.
        /// </summary>
        /// <param name="message">A message</param>
        /// <param name="geometry">A geometry</param>
        public SqlGeographyConverterException(string message, SMGeometry geometry)
            : base(message)
        {
            Geometry = geometry;
        }

        /// <summary>
        /// Creates an instance of this class providing a <paramref name="message"/>, an
        /// <paramref name="inner"/> exception and the failing <paramref name="geometry"/>.
        /// </summary>
        /// <param name="message">A message</param>
        /// <param name="inner">An inner exception</param>
        /// <param name="geometry">A geometry</param>
        public SqlGeographyConverterException(string message, Exception inner, SMGeometry geometry)
            : base(message, inner)
        {
            Geometry = geometry;
        }

        /// <summary>
        /// Creates an instance of this class using the provided <paramref name="info"/> and <paramref name="context"/>.
        /// </summary>
        /// <param name="info">A serialization info</param>
        /// <param name="context">A streaming context.</param>
        protected SqlGeographyConverterException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            //Geometry = (SMGeometry) info.GetValue("geom", typeof (SMGeometry));
        }

    }

    /// <summary>
    /// Utility class to convert from and to SqlServer geography objects
    /// </summary>
    public static class SqlGeographyConverter
    {
        private static readonly NetTopologySuite.NtsGeometryServices Services = NetTopologySuite.NtsGeometryServices.Instance;

        /// <summary>
        /// A reduction tolerance<br/>
        /// For SqlSever geography the tolerance is measured in the units defined by the unit_of_measure column of the  
        /// sys.spatial_reference_systems table corresponding to the SRID in which the instance is defined
        /// </summary>
        public static double ReduceTolerance = 1d;

        static SqlGeographyConverter()
        {
            SqlServer2008Ex.LoadSqlServerTypes();
        }

        /// <summary>
        /// Converts a geometry to a SqlServer geography
        /// </summary>
        /// <param name="smGeometry">A geometry</param>
        /// <returns>A geography</returns>
        public static SqlGeography ToSqlGeography(SMGeometry smGeometry)
        {
            SqlGeographyBuilder builder = new SqlGeographyBuilder();
            builder.SetSrid(smGeometry.SRID);

            SharpMapGeometryToSqlGeography(builder, smGeometry);

            SqlGeography g = builder.ConstructedGeography;
            if (!g.STIsValid())
            {
                try
                {
                    g = g.Reduce(ReduceTolerance);
                    g = g.MakeValid();
                }
                catch (Exception ex)
                {
                    throw new SqlGeographyConverterException(ex, smGeometry);
                }
            }

            if (!g.STIsValid())
                throw new SqlGeographyConverterException(smGeometry);

            return g;

        }

        /// <summary>
        /// Converts a series of geometries to SqlServer geographies.
        /// </summary>
        /// <param name="smGeometries">A series of geometries</param>
        /// <returns>A series of geographies</returns>
        public static IEnumerable<SqlGeography> ToSqlGeographies(IEnumerable<SMGeometry> smGeometries)
        {
            foreach (SMGeometry smGeometry in smGeometries)
                yield return ToSqlGeography(smGeometry);
        }

        private static void SharpMapGeometryToSqlGeography(SqlGeographyBuilder geogBuilder, SMGeometry smGeometry)
        {

            switch (smGeometry.OgcGeometryType)
            {
                case SMGeometryType.Point:
                    SharpMapPointToSqlGeography(geogBuilder, smGeometry as SMPoint);
                    break;
                case SMGeometryType.LineString:
                    SharpMapLineStringToSqlGeography(geogBuilder, smGeometry as SMLineString);
                    break;
                case SMGeometryType.Polygon:
                    SharpMapPolygonToSqlGeography(geogBuilder, smGeometry as SMPolygon);
                    break;
                case SMGeometryType.MultiPoint:
                    SharpMapMultiPointToSqlGeography(geogBuilder, smGeometry as SMMultiPoint);
                    break;
                case SMGeometryType.MultiLineString:
                    SharpMapMultiLineStringToSqlGeography(geogBuilder, smGeometry as SMMultiLineString);
                    break;
                case SMGeometryType.MultiPolygon:
                    SharpMapMultiPolygonToSqlGeography(geogBuilder, smGeometry as SMMultiPolygon);
                    break;
                case SMGeometryType.GeometryCollection:
                    SharpMapGeometryCollectionToSqlGeography(geogBuilder, smGeometry as SMGeometryCollection);
                    break;
                default:
                    throw new ArgumentException(
                        string.Format("Cannot convert '{0}' geography type", smGeometry.GeometryType), "smGeometry");
            }
        }

        private static void SharpMapGeometryCollectionToSqlGeography(SqlGeographyBuilder geogBuilder, SMGeometryCollection geometryCollection)
        {
            geogBuilder.BeginGeography(OpenGisGeographyType.GeometryCollection);
            for (int i = 0; i < geometryCollection.NumGeometries; i++)
                SharpMapGeometryToSqlGeography(geogBuilder, geometryCollection[i]);
            geogBuilder.EndGeography();
        }

        private static void SharpMapMultiPolygonToSqlGeography(SqlGeographyBuilder geogBuilder, SMMultiPolygon multiPolygon)
        {
            geogBuilder.BeginGeography(OpenGisGeographyType.MultiPolygon);
            for (int i = 0; i < multiPolygon.NumGeometries; i++)
                SharpMapPolygonToSqlGeography(geogBuilder, multiPolygon[i] as SMPolygon);
            geogBuilder.EndGeography();
        }

        private static void SharpMapMultiLineStringToSqlGeography(SqlGeographyBuilder geogBuilder, SMMultiLineString multiLineString)
        {
            geogBuilder.BeginGeography(OpenGisGeographyType.MultiLineString);
            for (int i = 0; i < multiLineString.NumGeometries; i++)
                SharpMapLineStringToSqlGeography(geogBuilder, multiLineString[i] as SMLineString);
            geogBuilder.EndGeography();
        }

        private static void SharpMapMultiPointToSqlGeography(SqlGeographyBuilder geogBuilder, SMMultiPoint multiPoint)
        {
            geogBuilder.BeginGeography(OpenGisGeographyType.MultiPoint);
            for (int i = 0; i < multiPoint.NumPoints; i++)
                SharpMapPointToSqlGeography(geogBuilder, multiPoint[i] as SMPoint);
            geogBuilder.EndGeography();
        }

        private static void SharpMapPointToSqlGeography(SqlGeographyBuilder geogBuilder, SMPoint point)
        {
            geogBuilder.BeginGeography(OpenGisGeographyType.Point);
            geogBuilder.BeginFigure(point.Y, point.X);
            geogBuilder.EndFigure();
            geogBuilder.EndGeography();
        }

        private static void SharpMapLineStringToSqlGeography(SqlGeographyBuilder geomBuilder, SMLineString lineString)
        {
            geomBuilder.BeginGeography(OpenGisGeographyType.LineString);
            var coords = lineString.Coordinates;
            geomBuilder.BeginFigure(coords[0].Y, coords[0].X);
            for (int i = 1; i < lineString.NumPoints; i++)
            {
                var point = coords[i];
                geomBuilder.AddLine(point.Y, point.X);
            }
            geomBuilder.EndFigure();
            geomBuilder.EndGeography();
        }

        private static void SharpMapPolygonToSqlGeography(SqlGeographyBuilder geogBuilder, SMPolygon polygon)
        {
            geogBuilder.BeginGeography(OpenGisGeographyType.Polygon);
            //Note: Reverse Exterior ring orientation
            AddRing(geogBuilder, (SMLinearRing)polygon.ExteriorRing.Reverse());
            for (int i = 0; i < polygon.NumInteriorRings; i++)
                AddRing(geogBuilder, (SMLinearRing)polygon.GetInteriorRingN(i));
            geogBuilder.EndGeography();
        }

        private static void AddRing(SqlGeographyBuilder builder, SMLinearRing linearRing)
        {
            if (linearRing.NumPoints < 3)
                return;

            //if (linearRing.Area == 0)
            //    return;

            var coords = linearRing.Coordinates;
            builder.BeginFigure(coords[0].Y, coords[0].X);
            for (var i = 1; i < linearRing.NumPoints; i++)
            {
                var pt = coords[i];
                builder.AddLine(pt.Y, pt.X);
            }
            builder.EndFigure();
        }

        /// <summary>
        /// Converts a SqlServer geography to a geometry as used in SharpMap
        /// </summary>
        /// <param name="geography">A geography</param>
        /// <returns>A geometry</returns>
        public static SMGeometry ToSharpMapGeometry(SqlGeography geography)
        {
            return ToSharpMapGeometry(geography, null);
        }

        /// <summary>
        /// Converts a SqlServer geography to a geometry as used in SharpMap. <br/>
        /// The <paramref name="factory"/> to use can be specified.
        /// </summary>
        /// <param name="geography">A geography</param>
        /// <param name="factory">The factory to use to create the result geometry.</param>
        /// <returns>A geometry</returns>
        public static SMGeometry ToSharpMapGeometry(SqlGeography geography, Factory factory)
        {
            if (geography == null) return null;
            if (geography.IsNull) return null;
            var fact = factory ?? Services.CreateGeometryFactory((int)geography.STSrid);

            if (geography.STIsEmpty())
                return fact.CreateGeometryCollection(null);

            if (!geography.STIsValid())
                geography = geography.MakeValid();

            OpenGisGeometryType geometryType = (OpenGisGeometryType)Enum.Parse(typeof(OpenGisGeometryType), (string)geography.STGeometryType());
            switch (geometryType)
            {
                case OpenGisGeometryType.Point:
                    return SqlGeographyToSharpMapPoint(geography, fact);
                case OpenGisGeometryType.LineString:
                    return SqlGeographyToSharpMapLineString(geography, fact);
                case OpenGisGeometryType.Polygon:
                    return SqlGeographyToSharpMapPolygon(geography, fact);
                case OpenGisGeometryType.MultiPoint:
                    return SqlGeographyToSharpMapMultiPoint(geography, fact);
                case OpenGisGeometryType.MultiLineString:
                    return SqlGeographyToSharpMapMultiLineString(geography, fact);
                case OpenGisGeometryType.MultiPolygon:
                    return SqlGeographyToSharpMapMultiPolygon(geography, fact);
                case OpenGisGeometryType.GeometryCollection:
                    return SqlGeographyToSharpMapGeometryCollection(geography, fact);
            }
            throw new ArgumentException(string.Format("Cannot convert SqlServer '{0}' to Sharpmap.Geometry", geography.STGeometryType()), "geography");
        }

        /// <summary>
        /// Converts a series of SqlServer geographies to geometries as used in SharpMap.
        /// </summary>
        /// <param name="sqlGeographies">A series of geographies</param>
        /// <returns>A series of geometries</returns>
        public static IEnumerable<SMGeometry> ToSharpMapGeometries(IEnumerable<SqlGeography> sqlGeographies)
        {
            return ToSharpMapGeometries(sqlGeographies, null);
        }

        /// <summary>
        /// Converts a series of SqlServer geographies to geometries as used in SharpMap. <br/>
        /// The <paramref name="factory"/> to use can be specified.
        /// </summary>
        /// <param name="sqlGeographies">A series of geographies</param>
        /// <param name="factory">The factory to use to create the result geometries.</param>
        /// <returns>A series of geometries</returns>
        public static IEnumerable<SMGeometry> ToSharpMapGeometries(IEnumerable<SqlGeography> sqlGeographies, Factory factory)
        {
            var fact = factory ?? Services.CreateGeometryFactory((int)sqlGeographies.First().STSrid);

            foreach (var sqlGeography in sqlGeographies)
                yield return ToSharpMapGeometry(sqlGeography, fact);
        }

        /*
        private static OpenGisGeometryType ParseGeometryType(string stGeometryType)
        {
            switch (stGeometryType.ToUpper())
            {
                case "POINT":
                    return OpenGisGeometryType.Point;
                case "LINESTRING":
                    return OpenGisGeometryType.LineString;
                case "POLYGON":
                    return OpenGisGeometryType.Polygon;
                case "MULTIPOINT":
                    return OpenGisGeometryType.MultiPoint;
                case "MULTILINESTRING":
                    return OpenGisGeometryType.MultiLineString;
                case "MULTIPOLYGON":
                    return OpenGisGeometryType.MultiPolygon;
                case "GEOMETRYCOLLECTION":
                    return OpenGisGeometryType.GeometryCollection;
            }
            throw new ArgumentException(String.Format("Invalid geometrytype '{0}'!", stGeometryType), "stGeometryType");
        }
        */

        private static SMGeometryCollection SqlGeographyToSharpMapGeometryCollection(SqlGeography geography, Factory factory)
        {
            var fact = factory ?? Services.CreateGeometryFactory((int)geography.STSrid);
            var geoms = new SMGeometry[(int)geography.STNumGeometries()];
            for (var i = 1; i <= geography.STNumGeometries(); i++)
                geoms[i - 1] = ToSharpMapGeometry(geography.STGeometryN(i), fact);
            return fact.CreateGeometryCollection(geoms);
        }

        private static SMMultiPolygon SqlGeographyToSharpMapMultiPolygon(SqlGeography geography, Factory factory)
        {
            var fact = factory ?? Services.CreateGeometryFactory((int)geography.STSrid);
            var polygons = new SMPolygon[(int)geography.STNumGeometries()];
            for (var i = 1; i <= geography.STNumGeometries(); i++)
                polygons[i - 1] = (SMPolygon)SqlGeographyToSharpMapPolygon(geography.STGeometryN(i), fact);
            return fact.CreateMultiPolygon(polygons);
        }

        private static SMMultiLineString SqlGeographyToSharpMapMultiLineString(SqlGeography geography, Factory factory)
        {
            var fact = factory ?? Services.CreateGeometryFactory((int)geography.STSrid);
            var lineStrings = new SMLineString[(int)geography.STNumGeometries()];
            for (var i = 1; i <= geography.STNumGeometries(); i++)
                lineStrings[i - 1] = (SMLineString)SqlGeographyToSharpMapLineString(geography.STGeometryN(i), fact);
            return fact.CreateMultiLineString(lineStrings);
        }

        private static SMGeometry SqlGeographyToSharpMapMultiPoint(SqlGeography geography, Factory factory)
        {
            var fact = factory ?? Services.CreateGeometryFactory((int)geography.STSrid);
            var points = new SMPoint[(int)geography.STNumGeometries()];
            for (var i = 1; i <= geography.STNumGeometries(); i++)
                points[i - 1] = (SMPoint)SqlGeographyToSharpMapPoint(geography.STGeometryN(i), fact);
            return fact.CreateMultiPoint(points);
        }

        private static SMGeometry SqlGeographyToSharpMapPoint(SqlGeography geography, Factory factory)
        {
            var fact = factory ?? Services.CreateGeometryFactory((int)geography.STSrid);
            return fact.CreatePoint(new Coordinate((double)geography.Long, (double)geography.Lat));
        }

        private static Coordinate[] GetPoints(SqlGeography geography)
        {
            var pts = new Coordinate[(int)geography.STNumPoints()];
            for (int i = 1; i <= (int)geography.STNumPoints(); i++)
            {
                var ptGeometry = geography.STPointN(i);
                pts[i - 1] = new Coordinate((double)ptGeometry.Long, (double)ptGeometry.Lat);
            }
            return pts;
        }

        private static SMGeometry SqlGeographyToSharpMapLineString(SqlGeography geography, Factory factory)
        {
            var fact = factory ?? Services.CreateGeometryFactory((int)geography.STSrid);
            return fact.CreateLineString(GetPoints(geography));
        }

        private static SMGeometry SqlGeographyToSharpMapPolygon(SqlGeography geography, Factory factory)
        {
            var fact = factory ?? Services.CreateGeometryFactory((int)geography.STSrid);

            // courtesy of NetTopologySuite.Io.SqlServerBytes
            var rings = new List<LinearRing>();
            for (var i = 1; i <= geography.NumRings(); i++)
                rings.Add(fact.CreateLinearRing(GetPoints(geography.RingN(i))));

            var shellCCW = rings.FirstOrDefault(r => r.IsCCW);
            // NB: reverse exterio ring orientation
            var shellCW = fact.CreateLinearRing(shellCCW.Reverse().Coordinates);

            return fact.CreatePolygon(shellCW, Enumerable.ToArray(rings.Where(r => r != shellCCW)));
        }

    }
}

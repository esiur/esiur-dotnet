#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Esiur.Data;

/// <summary>
/// Base class for local, schema-less types whose members are identified by byte indexes.
/// Structures use the same wire representation as <c>Map&lt;byte, object&gt;</c>, but can be
/// composed and parsed directly as CLR types through <see cref="Codec"/>.
/// </summary>
public abstract class IndexedStructure
{

}


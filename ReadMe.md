## KdSoft.Lmdb

A .NET wrapper for  OpenLDAP's [LMDB](https://github.com/LMDB/lmdb) key-value store.
It introduces the use of the `Span<byte>` type for interacting with the native LMDB library in order to reduce the need for copying byte buffers between native and managed scope.
Cursor iteration (even with foreach) is allocation free.

The native C-API is exposed in a .NET typical way, so its use should be familiar to .NET developers.

It requires the .NET Core SDK 2.1 (or later) installed. There are no third-party dependencies.

## Code Example

#### Create a database

```c#
var envConfig = new LmdbEnvironmentConfiguration(10);
using (var env = new LmdbEnvironment(envConfig)) {
    env.Open(envPath);    
    Database dbase;
    var dbConfig = new DatabaseConfiguration(DatabaseOptions.Create);
    using (var tx = env.BeginDatabaseTransaction(TransactionModes.None)) {
        dbase = tx.OpenDatabase("TestDb1", dbConfig);
        tx.Commit();
    }
    // use dbase from here on
}
```

#### Simple Store and Retrieve

```c#
<Env points to an open LmdbEnvironment handle>
...  
var config = new DatabaseConfiguration(DatabaseOptions.Create);
Database dbase;
using (var tx = Env.BeginDatabaseTransaction(TransactionModes.None)) {
    dbase = tx.OpenDatabase("SimpleStoreRetrieve", config);
    tx.Commit();
}

int key = 234;
var keyBuf = BitConverter.GetBytes(key);
string putData = "Test Data";

using (var tx = Env.BeginTransaction(TransactionModes.None)) {
    dbase.Put(tx, keyBuf, Encoding.UTF8.GetBytes(putData), PutOptions.None);
    tx.Commit();
}

ReadOnlySpan<byte> getData;
using (var tx = Env.BeginReadOnlyTransaction(TransactionModes.None)) {
    Assert.True(dbase.Get(tx, keyBuf, out getData));
    tx.Commit();
}

Assert.Equal(putData, Encoding.UTF8.GetString(getData));
```

#### Cursor Operations - Single-Value Database

```c#
<Dbase points to an open Database handle, tx is an open transaction>
...
// basic iteration
using (var cursor = Dbase.OpenCursor(tx)) {
    foreach (var entry in cursor.Forward) {  // cursor.Reverse goes the other way
        var key = BitConverter.ToInt32(entry.Key);
        var data = Encoding.UTF8.GetString(entry.Data);
    }
}

// move cursor to key position and get data forward from that key
using (var cursor = Dbase.OpenCursor(tx)) {
    var keyBytes = BitConverter.GetBytes(1874);
    if (cursor.MoveToKey(keyBytes)) {
        Assert.True(cursor.GetCurrent(out KeyDataPair entry));
        var dataString = Encoding.UTF8.GetString(entry.Data);
        while (cursor.GetNext(...)) {
            //
        }
    }
}

// iterate over key range (using foreach)
using (var cursor = Dbase.OpenCursor(tx)) {
    var startKeyBytes = BitConverter.GetBytes(33);
    Assert.True(cursor.MoveToKey(startKeyBytes));

    var endKeyBytes = BitConverter.GetBytes(99);
    foreach (var entry in cursor.ForwardFromCurrent) {
        // test for end of range (> 0 or >=0)
        if (Dbase.Compare(tx, entry.Key, endKeyBytes) > 0)
            break;

        var ckey = BitConverter.ToInt32(entry.Key);
        var cdata = Encoding.UTF8.GetString(entry.Data);
                        
        Console.WriteLine($"{ckey}: {cdata}");
    }
}

```
#### Cursor Operations - Multi-Value Database
```c#
// iteration over multi-value database
using (var cursor = Dbase.OpenMultiValueCursor(tx)) {
    foreach (var keyEntry in cursor.ForwardByKey) {
        var key = BitConverter.ToInt32(keyEntry.Key);
        var valueList = new List<string>();
        // iterate over the values in the same key
        foreach (var value in cursor.ValuesForward) {
            var data = Encoding.UTF8.GetString(value);
            valueList.Add(data);
        }
    }
}

// move to key, iterate over multiple values for key
using (var cursor = Dbase.OpenMultiValueCursor(tx)) {
    Assert.True(cursor.MoveToKey(BitConverter.GetBytes(234));
    var valueList = new List<string>();
    foreach (var value in cursor.ValuesForward) {
        var data = Encoding.UTF8.GetString(value);
        valueList.Add(data);
    }
}

// Move to key *and* nearest data in multi-value database
using (var cursor = Dbase.OpenMultiValueCursor(tx)) {
    var dataBytes = Encoding.UTF8.GetBytes("Test Data");
    var keyData = new KeyDataPair(BitConverter.GetBytes(4), dataBytes);
    KeyDataPair entry;  // the key-value pair nearest to keyData
    Assert.True(cursor.GetNearest(keyData, out entry));
    var dataString = Encoding.UTF8.GetString(entry.Data);
}
```

The unit tests have more examples, especially for cursor operations.

## Motivation

Provide a .NET/C# friendly API and add support for zero-copy access to the native library.

## Installation

Include as Nuget package from https://www.nuget.org/packages/KdSoft.Lmdb/ . This is not quite sufficient on platforms other than Windows:

#### Installing the native libraries
* Windows: A recent x64 build is included in this project.

  * Note: On Windows the data file will be pre-allocated to the full size of the memory map. If one needs the capability to have the data file grow incrementally, then one must use a build from the __master__ branch on https://github.com/LMDB/lmdb.git . This has about a 10%-20% performance penalty. See related issue [OpenLDAP ITS#8324](https://www.openldap.org/its/index.cgi/Software%20Enhancements?id=8324;selectid=8324) which is "fixed" on the master branch.

* Linux-like: 

  * Install package, Example for Ubuntu: `sudo apt-get install liblmdb-dev`
  * Install from source, like in this example

  ```
  git clone https://github.com/LMDB/lmdb
  cd lmdb/libraries/liblmdb
  make && make install
  ```

## API Reference

API documentation can be found at  https://kwaclaw.github.io/KdSoft.Lmdb/.

The native API is documented here: http://www.lmdb.tech/doc/ .

## Tests

On non-Windows platforms, LMDB must already be installed so that it can be looked up
by DLLImport using the platform-typical name.

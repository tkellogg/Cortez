The LINQ Fast & Friendly Object-To-Object Mapper
================================================

Cortez is as fast as it gets for object-to-object mappers. It uses the
Expression tree enhancements from .NET 4.0 to generate functions 
to map one object to another.

```csharp
var mapper = new Cortez();
var viewModel = mapper.Map<User, UserViewModel>(existingUser);
```

LINQ to Databases
-----------------

Calling Cortez an _"Object to Object"_ mapper is a bit of a misnomer since
the source doesn't have to be an object. Since mappings are generated from
Expression trees, you can also retrieve just the expression tree, which 
makes Cortez _really_ convenient to use with LINQ.

```csharp
var mapper = new Cortez();
selectExpression = mapper.GetExpression<User, UserViewModel>();

// UserViewModel is populated directly from the database, 
// no user object is instantiated
var viewModel = users.AsQueryable()
    .Where(u => u.Name == "Jack")
    .Select(selectExpression);
```

Using Cortez
------------

It's a single file, just download it [here][1] and include it in your
project.

 [1]: 
://github.com/tkellogg/Cortez/blob/master/Cortez/Cortez.csar mapper = new Cortez();

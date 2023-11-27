# SoftballTech-Cosmos-EF (Razor)

This is a learning project for myself, and it is not expected to be useful to the world at large. However, my LI post discusses some speed bumps that I encountered that might be interesting for others just learning about Cosmos. 

For this project, I modified my first Cosmos project to change the database access code from Cosmos SDK to Entity Framework Core. (Please see the ReadMe in the original repo for more info about the website itself.)

This transition was not as simple as I imagined it would be. I write about the issues in my LinkedIn post (see the end of this ReadMe for a link).

The biggest hurdle I faced was thinking that EF Core could simply read the data written by the Cosmos SDK, which does not seem to be true. 

On the other hand, the EF Core code is much simpler than the prior version (or maybe I didn't know how to write simple code using that SDK).

As with the others, this version is running on Azure, and can be found [here](https://sbt-cosmos-ef.azurewebsites.net/).

The LinkedIn discussion post is [here](https://www.linkedin.com/feed/update/urn:li:activity:7134971126622957568/).

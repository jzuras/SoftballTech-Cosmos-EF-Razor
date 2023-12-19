# SoftballTech-Cosmos-EF (Razor)

This is a learning project for myself, and it is not expected to be useful to the world at large. However, my LI post discusses some speed bumps that I encountered that might be interesting for others just learning about Cosmos. 

For this project, I modified my first Cosmos project to change the database access code from Cosmos SDK to Entity Framework Core. (Please see the ReadMe in the original repo for more info about the website itself.)

This transition was not as simple as I imagined it would be. I write about the issues in my LinkedIn post (see the end of this ReadMe for a link).

The biggest hurdle I faced was thinking that EF Core could read the data written by the Cosmos SDK, which is not true. 

On the other hand, the EF Core code is much simpler than the prior version (or maybe I didn't know how to write simple code using that SDK).

Originally, this was running on Azure just like my other projects. However, that is no longer the case due to a redesign of the data model for a subsequent version that uses MVC. These two projects had to share a Cosmos Container due to the limits of the free version, and the data model change breaks this version. The MVC version is so much better than this one now, so it is not worth the time to make this work with the new data model.

The LinkedIn discussion post is [here](https://www.linkedin.com/feed/update/urn:li:activity:7134971126622957568/).

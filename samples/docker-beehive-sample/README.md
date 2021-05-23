Execute the sample
==================

Environment has been built with `docker-compose`.  
It created a MongoDB and a Beehive Manager instance with a dedicated shared network.

Run it with:

```
docker-compose up -d
```

The Swagger Api client will be accessible at http://localhost/swagger/.

The [Hangfire](https://www.hangfire.io/) console (the async engine) is accessible at http://localhost/admin/hangfire/.

Configuration
-------------

Application can be configured using environement variables:

* `ConnectionStrings__BeehiveManagerDb`: application's main db connection string
* `ConnectionStrings__HangfireDb`: async engine db connection string
* `ConnectionStrings__SystemDb`: system configuration db connection string

Destroy
=======

If you want to destroy the environment use:

```
docker-compose down
```

This will not remove mongo volumes, that have to be removed manually:

```
docker volume rm docker-beehive-sample_mongo-configdb
docker volume rm docker-beehive-sample_mongo-db
```

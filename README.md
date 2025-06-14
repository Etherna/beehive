# Beehive

## Overview

Beehive is a utility to manage a cluster of [Bee Swarm](https://github.com/ethersphere/bee) nodes.

Beehive exposes a REST Api that permits to register and interact programmatically with different Bee nodes.  
It runs also an async task engine that can run cron operations, like automatic scheduled cashout.

It uses a MongoDB instance for keep cluster configuration.

## Docker images

You can get latest stable and unstable releases from our [Docker Hub repository](https://hub.docker.com/r/etherna/beehive).

You can find a sample on how to run with Docker Compose [here](samples/docker-beehive-sample)

## Issue reports

If you've discovered a bug, or have an idea for a new feature, please report it to our issue manager based on Jira https://etherna.atlassian.net/projects/BHM.

Detailed reports with stack traces, actual and expected behaviours are welcome.

## Questions? Problems?

For questions or problems please write an email to [info@etherna.io](mailto:info@etherna.io).

## License

![AGPL Logo](https://www.gnu.org/graphics/agplv3-with-text-162x68.png)

We use the GNU Affero General Public License v3 (AGPL-3.0) for this project.
If you require a custom license, you can contact us at [license@etherna.io](mailto:license@etherna.io).

# RemoteTasks
such as publish, upload ...

## deploy
```shell
if [[ $v == "all" ]] || [[ $v =~ "test" ]]; then
    cd /home/administrator/web/remote.test
    docker stop remote.test
    docker rm remote.test

    #docker rmi remote.test
    #docker build -t remote.test .

    docker run -d  -p 8989:8989 -p 5105:5015 -p 7166:7166 --name remote.test remote.test
    docker update --restart=always remote.test
fi
```
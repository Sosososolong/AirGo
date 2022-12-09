# RemoteTasks
such as publish, upload ...

## deploy
```shell
if [[ $v == "all" ]] || [[ $v =~ "test" ]]; then
    cd /home/administrator/web/iduo.test
    docker stop iduo.test
    docker rm iduo.test

    #docker rmi iduo.test
    #docker build -t iduo.test .

    docker run -d -p 5105:80 -p 7166:7166 --name iduo.test iduo.test
    docker update --restart=always iduo.test
fi
```



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

## frontend
### global function `execute`: Request backend api
- Command button (request backend api)
  - For example, this is a update button
  ```html
  <button class="btn btn-primary btn-sm d-none"
    type="button"
    data-table-id="${tableId}"
    data-content="&quot;${record.id}&quot;"
    data-execute-url="/Xxx/Xxx"
    data-method="POST"
    onclick="showConfirmBox('Are you sure you want to update?', () => execute(this))">Update</button>
  ```
    - `data-execute-url`: api url
    - `data-content`
      - Parameters in the request body (HTML escaping is required here, which might be a bit inconvenient, so this is suitable for API calls with simple parameters.)
        - For example, if you only need to pass an ID here, it might be data-content="&quot;${record.id}&quot;".
        - If the request content-type is application/json, it might look like this - data-content="{&quot;id&quot;:1}".
      - For complex parameters, data-content supports including a list of form item IDs corresponding to the parameters, in the format formItemIds:formItem1Id;formItem2Id;...
        - For example, in a data update scenario where I only need to update the age attribute, I only need to pass the data ID and the new age value. Therefore, I should have an input control with the name attribute 'age' and a hidden field with the name attribute 'id' (used to record the ID). Assuming their IDs are 'age' and 'id' respectively, the data-content should be data-content="formItemIds:age;id".
        - By the way, our backend supports partial updates. All you need to do is use the repository base class `RepositoryBase<T>` and specify the generic entity class. All parameters for the partial update, including the data ID and the fields to be updated, should be placed in a single dictionary.
    - `data-method`: GET/POST
    - `onclick`: The request interface logic is all in the `execute` function. Here, the main thing is to trigger the `execute` function on click; a confirmation box is added here, and the `execute` function is executed after confirmation

- Additionally, the execute function supports passing a plain object, as long as these objects have the corresponding properties (dataTableId, dataContent, dataExecuteUrl, dataMethod).
function docker-connect() {
  docker exec -ti --env COLUMNS=`tput cols` --env LINES=`tput lines` $1 bash -l
}

function paket() {
  dotnet paket "$@" 
}

function build() {
  dotnet fake build -t build
}

function run-tests() {
  dotnet fake build -t runTests
}

function run() {
  dotnet fake build -t run
}

function watch-tests() {
  dotnet watch -p tests/Tests/ run
}



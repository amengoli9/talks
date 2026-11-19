variable "TRUSTED_CA" { default = "" }
group "default" {
  targets = ["standard", "chiseled", "artifacts"]
}
target "common" {
  context = "."
  dockerfile = "containers/Dockerfile"
  platforms = ["linux/amd64"]
  secret = TRUSTED_CA == "" ? [] : ["id=trusted_ca,src=${TRUSTED_CA}"]
}
target "standard" {
  inherits = ["common"]
  target = "standard"
  tags = ["babylog:standard"]
  output = ["type=docker"]
}
target "chiseled" {
  inherits = ["common"]
  target = "chiseled"
  tags = ["babylog:chiseled"]
  output = ["type=docker"]
}
target "artifacts" {
  inherits = ["common"]
  target = "artifacts"
  output = ["type=local,dest=.artifacts/publish"]
}

# Imported from ru-RU-additional; existing Russian keys are preserved.

ent-ConjuredObject10 = { "" }
    .desc = A magically created entity, that'll vanish from existence eventually.
    .suffix = Conjured

ent-SoapConjured = soap
    .desc = { ent-BaseBullet.desc }

ent-SoapletBloodCult = soaplet
    .desc = { ent-SoapConjured.desc }

ent-ShellSoapConjuredBloodCultCluster = { ent-SoapConjured }
    .desc = { ent-SoapConjured.desc }

ent-SoapletBloodCultSpread = { ent-SoapletBloodCult }
    .desc = { ent-SoapletBloodCult.desc }
